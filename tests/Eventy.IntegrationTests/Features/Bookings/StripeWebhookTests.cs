using System.Net;
using System.Net.Http.Json;
using System.Text;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Bookings;

[Collection("Integration")]
public class StripeWebhookTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public StripeWebhookTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string ComputeStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    [Fact]
    public async Task StripeWebhook_WhenCheckoutSessionCompleted_ShouldConfirmBooking()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var booking = await dbBefore.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending);
        var bookingId = booking.Id.Value;

        var webhookSecret = _fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Payments.StripeSettings>>()
            .Value.WebhookSecret;

        var sessionPayload = $@"{{
            ""id"": ""evt_test_{Guid.NewGuid():N}"",
            ""object"": ""event"",
            ""type"": ""checkout.session.completed"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""cs_test_{Guid.NewGuid():N}"",
                    ""object"": ""checkout.session"",
                    ""payment_status"": ""paid"",
                    ""metadata"": {{
                        ""bookingId"": ""{bookingId}""
                    }}
                }}
            }}
        }}";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(sessionPayload, webhookSecret, timestamp);

        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(sessionPayload, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("Stripe-Signature", signature);

        var webhookResponse = await _client.SendAsync(webhookRequest);
        if (webhookResponse.StatusCode != HttpStatusCode.OK)
        {
            var body = await webhookResponse.Content.ReadAsStringAsync();
            webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"webhook should accept a validly-signed checkout.session.completed event. " +
                $"Response body: {body}");
        }

        await using var dbAfter = _fixture.CreateDbContext();
        var confirmedBooking = await dbAfter.Db.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id);
        confirmedBooking.Should().NotBeNull();
        confirmedBooking!.Status.Should().Be(BookingStatusEnum.Confirmed,
            "booking should be confirmed after Stripe webhook confirms payment");
    }

    [Fact]
    public async Task StripeWebhook_WhenInvalidSignature_ShouldReturnBadRequest()
    {
        var payload = @"{""id"":""evt_test"",""object"":""event"",""type"":""checkout.session.completed""}";
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("Stripe-Signature", "t=123,v1=invalid");

        var response = await _client.SendAsync(webhookRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invalid signature should be rejected");
    }

    [Fact]
    public async Task StripeWebhook_WhenUnhandledEventType_ShouldReturn202()
    {
        var webhookSecret = _fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Payments.StripeSettings>>()
            .Value.WebhookSecret;

        var payload = $@"{{
            ""id"": ""evt_test_{Guid.NewGuid():N}"",
            ""object"": ""event"",
            ""type"": ""payment_intent.payment_failed"",
            ""data"": {{ ""object"": {{}} }}
        }}";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(payload, webhookSecret, timestamp);

        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("Stripe-Signature", signature);

        var response = await _client.SendAsync(webhookRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "unhandled event types should return 202 Accepted, not 200 OK");
    }

    [Fact]
    public async Task StripeWebhook_WhenSessionNotPaid_ShouldReturn202()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var booking = await dbBefore.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        var bookingId = booking!.Id.Value;

        var webhookSecret = _fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Payments.StripeSettings>>()
            .Value.WebhookSecret;

        var payload = $@"{{
            ""id"": ""evt_test_{Guid.NewGuid():N}"",
            ""object"": ""event"",
            ""type"": ""checkout.session.completed"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""cs_test_{Guid.NewGuid():N}"",
                    ""object"": ""checkout.session"",
                    ""payment_status"": ""unpaid"",
                    ""metadata"": {{
                        ""bookingId"": ""{bookingId}""
                    }}
                }}
            }}
        }}";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(payload, webhookSecret, timestamp);

        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("Stripe-Signature", signature);

        var response = await _client.SendAsync(webhookRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "unpaid sessions should return 202 Accepted, not 200 OK — so Stripe doesn't think it was fully processed");

        await using var dbAfter = _fixture.CreateDbContext();
        var unconfirmedBooking = await dbAfter.Db.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id);
        unconfirmedBooking!.Status.Should().Be(BookingStatusEnum.Pending,
            "booking should remain pending when payment status is unpaid");
    }
}
