using System.Net;
using System.Text;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
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
/// Tests the combined race: booking created → outbox dispatches BookingCreatedEvent
/// (auto-confirms Instant bookings) → Stripe webhook arrives simultaneously.
/// Both paths (outbox handler + webhook handler) call Confirm() on the same booking.
/// The idempotency guards must prevent double-confirmation.
/// </summary>
[Collection("Concurrency")]
public class OutboxWebhookRaceTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public OutboxWebhookRaceTests(ConcurrencyTestFixture fixture)
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
    public async Task OutboxAndWebhook_SimultaneousConfirmation_ShouldNotDoubleConfirm()
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
        var result = await executor.ExecuteAsync(2, async (int workerIndex) =>
        {
            if (workerIndex == 0)
            {
                using var outboxScope = _fixture.Factory.Services.CreateScope();
                var outboxRepo = outboxScope.ServiceProvider.GetRequiredService<Application.Abstractions.Outbox.IOutboxRepository>();
                var dispatcher = outboxScope.ServiceProvider.GetRequiredService<Application.Abstractions.Outbox.IOutboxDispatcher>();
                var timeProvider = outboxScope.ServiceProvider.GetRequiredService<TimeProvider>();
                var testLockId = Guid.NewGuid();
                var messages = await outboxRepo.GetAndLockUnprocessedMessagesAsync(
                    testLockId, timeProvider, 50, CancellationToken.None);
                if (messages.Count > 0)
                    await dispatcher.DispatchBatchAsync(messages, testLockId, timeProvider, CancellationToken.None);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            else
            {
                var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
                {
                    Content = new StringContent(sessionPayload, Encoding.UTF8, "application/json")
                };
                webhookRequest.Headers.Add("Stripe-Signature", signature);
                return await _client.SendAsync(webhookRequest);
            }
        });

        result.SuccessCount.Should().BeGreaterThanOrEqualTo(1,
            "at least one path should succeed");

        await using var dbAfter = _fixture.CreateDbContext();
        var confirmedBooking = await dbAfter.DbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id);
        confirmedBooking!.Status.Should().Be(BookingStatusEnum.Confirmed,
            "booking should be confirmed by whichever path won the race");

        var eventAfter = await dbAfter.DbContext.Events
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        var ticketTypeAfter = eventAfter!.TicketTypes.First();
        ticketTypeAfter.SoldCount.Should().Be(2,
            "SoldCount should be exactly 2 (the booking quantity) — not 4 from double-confirmation");
        ticketTypeAfter.ReservedCount.Should().Be(0,
            "ReservedCount should be 0 — not negative from double-decrement");
    }
}
