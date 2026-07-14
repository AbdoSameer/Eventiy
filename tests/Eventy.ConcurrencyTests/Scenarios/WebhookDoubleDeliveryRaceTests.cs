using System.Net;
using System.Net.Http.Json;
using System.Text;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Stripe guarantees at-least-once webhook delivery. This test verifies that
/// 5 concurrent webhook deliveries for the same booking confirm it exactly once.
/// The idempotency guard in ConfirmBookingFromWebhookCommandHandler must prevent
/// double-confirmation (double-decrement of ReservedCount, double-increment of SoldCount).
/// </summary>
[Collection("Concurrency")]
public class WebhookDoubleDeliveryRaceTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public WebhookDoubleDeliveryRaceTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    private static string ComputeStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    [Fact]
    public async Task WebhookDoubleDelivery_5ConcurrentForSameBooking_ShouldConfirmExactlyOnce()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 10);

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var booking = await dbBefore.DbContext.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending);
        var bookingId = booking.Id.Value;

        var webhookSecret = _fixture.Factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Payments.StripeSettings>>()
            .Value.WebhookSecret;

        var stripeEventId = $"evt_test_{Guid.NewGuid():N}";

        var sessionPayload = $@"{{
            ""id"": ""{stripeEventId}"",
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

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(5, async (_) =>
        {
            var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
            {
                Content = new StringContent(sessionPayload, Encoding.UTF8, "application/json")
            };
            webhookRequest.Headers.Add("Stripe-Signature", signature);
            return await _client.SendAsync(webhookRequest);
        });

        result.SuccessCount.Should().BeGreaterThanOrEqualTo(1,
            "at least one webhook delivery should succeed");

        await using var dbAfter = _fixture.CreateDbContext();
        var confirmedBooking = await dbAfter.DbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id);
        confirmedBooking!.Status.Should().Be(BookingStatusEnum.Confirmed,
            "booking should be confirmed after webhook delivery");

        var eventAfter = await dbAfter.DbContext.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        eventAfter.Should().NotBeNull();
        var ticketTypeAfter = eventAfter!.TicketTypes.First();
        ticketTypeAfter.SoldCount.Should().Be(2,
            "SoldCount should be exactly the booking quantity (2), not double (4) — idempotency guard prevents double-confirmation");
        ticketTypeAfter.ReservedCount.Should().Be(0,
            "ReservedCount should be 0 after confirmation — not negative from double-decrement");
    }
}
