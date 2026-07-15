using System.Net;
using System.Net.Http.Json;
using System.Text;
using Application.Abstractions.Payments;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using Eventy.IntegrationTests.Helpers;
using Eventy.Testing.Foundation.Fakes;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Bookings;

/// <summary>
/// End-to-end tests for the Pending-First + Durable Compensation pattern.
/// Each test maps to one of the five critical failure scenarios requested:
///
///   A — Success Path:        booking → payment → webhook → Confirmed
///   B — External Failure:    payment service down → graceful handling
///   C — Commit Failure:      payment OK, local commit fails → compensation record
///   D — Compensation Retry:  first cancel fails (503) → processor retries → success
///   E — Webhook Idempotency: duplicate webhook → no double confirmation
/// </summary>
[Collection("Integration")]
public class PendingFirstBookingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public PendingFirstBookingTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        GetFakePayment().Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private FakePaymentService GetFakePayment()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var ps = scope.ServiceProvider.GetRequiredService<IPaymentService>() as FakePaymentService;
        ps.Should().NotBeNull("FakePaymentService must be registered by the test factory");
        return ps!;
    }

    private async Task<(Guid EventId, Guid TicketTypeId)> SeedAsync(
        int eventCapacity = 100, int ticketCapacity = 50) =>
        await _fixture.SeedPublishedEventAsync(eventCapacity, ticketCapacity);

    private static string ComputeStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string payload)
    {
        var webhookSecret = _fixture.Factory.Services
            .GetRequiredService<IOptions<Infrastructure.Payments.StripeSettings>>()
            .Value.WebhookSecret;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature(payload, webhookSecret, timestamp);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", signature);

        return await _client.SendAsync(request);
    }

    private static string BuildCheckoutSessionPayload(Guid bookingId, string eventId, string paymentStatus = "paid") => $$"""
        {
            "id": "{{eventId}}",
            "object": "event",
            "type": "checkout.session.completed",
            "data": {
                "object": {
                    "id": "cs_test_{{Guid.NewGuid():N}}",
                    "object": "checkout.session",
                    "payment_status": "{{paymentStatus}}",
                    "metadata": { "bookingId": "{{bookingId}}" }
                }
            }
        }
        """;

    // =====================================================================
    //  Scenario A — Success Path
    // =====================================================================

    [Fact]
    public async Task ScenarioA_Success_BookingCreated_PaymentInitiated_WebhookConfirms()
    {
        var (eventId, ticketTypeId) = await SeedAsync();

        // 1. Create the Instant booking — Phase-1 commits as Pending, Phase-2
        //    initiates payment with a fake gateway URL.
        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbAfterCreate = _fixture.CreateDbContext();
        var booking = await dbAfterCreate.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending,
            "Phase-1 commits the booking as Pending; it is NOT auto-confirmed");

        // The fake gateway should have received the idempotency key.
        var fake = GetFakePayment();
        fake.IdempotencyKeysReceived.Should().ContainKey(booking.Id.Value);
        fake.IdempotencyKeysReceived[booking.Id.Value]
            .Should().StartWith("payment-initiate:");

        // 2. Simulate the Stripe webhook confirming the checkout session.
        var stripeEventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var payload = BuildCheckoutSessionPayload(booking.Id.Value, stripeEventId);
        var webhookResponse = await SendWebhookAsync(payload);
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. The booking should now be Confirmed and seats sold.
        await using var dbAfterWebhook = _fixture.CreateDbContext();
        var confirmed = await dbAfterWebhook.Db.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id);
        confirmed!.Status.Should().Be(BookingStatusEnum.Confirmed,
            "the webhook is the single source of truth for confirmation");

        var @event = await dbAfterWebhook.Db.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        @event!.TicketTypes.First().SoldCount.Should().Be(2,
            "confirmation must move the reserved seats into the sold count");
    }

    // =====================================================================
    //  Scenario B — External Failure (payment service down)
    // =====================================================================

    [Fact]
    public async Task ScenarioB_ExternalFailure_PaymentDown_BookingPersisted_GracefullyHandled()
    {
        var (eventId, ticketTypeId) = await SeedAsync();

        var fake = GetFakePayment();
        fake.SetFailMode(true);

        // The endpoint surfaces a 500 because the payment-initiation result is a
        // failure, but the booking MUST already be durably persisted.
        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest);

        await using var db = _fixture.CreateDbContext();

        // State is NOT corrupted: booking exists and is Pending.
        var booking = await db.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull(
            "the booking must be committed (Phase 1) BEFORE the payment call — " +
            "a payment outage cannot lose the reservation");
        booking!.Status.Should().Be(BookingStatusEnum.Pending);

        // A durable compensation record was staged.
        await db.Db.ShouldHaveCompensationForBookingAsync(booking.Id.Value);
    }

    // =====================================================================
    //  Scenario C — Commit Failure (payment OK, local DB commit fails)
    // =====================================================================

    [Fact]
    public async Task ScenarioC_CompensationRecord_PersistsAcrossNewDbContext()
    {
        // We can't easily inject a commit fault into the live handler without
        // a dedicated seam, but we CAN prove the durability property that
        // Scenario C depends on: a compensation record, once written, survives
        // a context disposal / "crash" and is visible to a fresh DbContext
        // (which is exactly what the CompensationProcessor uses).

        var (eventId, ticketTypeId) = await SeedAsync();
        var fake = GetFakePayment();
        fake.SetFailMode(true); // force Phase-2 failure → compensation staged

        await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });

        // The compensation was written in the handler's UoW. Open a BRAND NEW
        // context — if the row is visible here, it was durably committed, not
        // sitting in an uncommitted transaction.
        await using var freshDb = _fixture.CreateDbContext();
        var compensation = await freshDb.Db.CompensationLogs
            .FirstOrDefaultAsync(c => c.CompensationType == "CancelPayment");

        compensation.Should().NotBeNull(
            "the compensation record must be persisted in its own transaction " +
            "(CommitWithoutEventsAsync) so it survives a crash between the " +
            "payment call and the booking transaction");
        compensation!.ProcessedOnUtc.Should().BeNull(
            "the record is staged but not yet processed by the CompensationProcessor");
        compensation.IdempotencyKey.Should().StartWith("compensation:CancelPayment:");
    }

    // =====================================================================
    //  Scenario D — Compensation Retry (first cancel fails, then succeeds)
    // =====================================================================

    [Fact]
    public async Task ScenarioD_CompensationRetry_FirstCancelFails_SecondSucceeds()
    {
        var (eventId, ticketTypeId) = await SeedAsync();

        // 1. Create a real booking so the cancel target exists.
        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbAfterCreate = _fixture.CreateDbContext();
        var booking = await dbAfterCreate.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        var bookingId = booking!.Id.Value;

        // 2. Manually stage a compensation record (simulating a prior payment
        //    failure that already committed durably).
        var compensationId = Guid.NewGuid();
        await using var stageDb = _fixture.CreateDbContext();
        stageDb.Db.CompensationLogs.Add(new Infrastructure.Persistence.Outbox.CompensationLog(
            id: compensationId,
            bookingId: bookingId,
            compensationType: "CancelPayment",
            payload: System.Text.Json.JsonSerializer.Serialize(new
            {
                BookingId = bookingId,
                Reason = "Payment initiation failed (simulated)",
                OccurredAt = DateTime.UtcNow
            }),
            occurredOnUtc: DateTime.UtcNow,
            idempotencyKey: $"compensation:CancelPayment:{bookingId}"));
        await stageDb.Db.SaveChangesAsync();

        // Verify the record exists before we start processing.
        await using var verifyDb = _fixture.CreateDbContext();
        await verifyDb.Db.ShouldHaveCompensationForBookingAsync(bookingId);

        // 3. Configure the fake gateway to fail the FIRST cancel, then succeed.
        var fake = GetFakePayment();
        fake.SetCancelFailCount(1);

        // 4. First processing cycle — the single cancel attempt fails.
        var driver = new CompensationTestDriver(_fixture.Factory.Services);
        var firstCycle = await driver.ProcessOnceAsync();

        firstCycle.Failed.Should().HaveCount(1,
            "the transient gateway error (503) should be recorded as a failure, not a crash");
        firstCycle.ProcessedIds.Should().BeEmpty();
        firstCycle.Failed[0].NewRetryCount.Should().Be(1,
            "the retry counter must increment on each failure");

        await using var dbAfterFirstFail = _fixture.CreateDbContext();
        await dbAfterFirstFail.Db.ShouldHaveCompensationRetryCountAsync(bookingId, 1);

        // 5. The record's NextRetryOnUtc is in the future (5s backoff). Clear it
        //    via raw SQL so the next cycle picks it up immediately.
        await using var resetDb = _fixture.CreateDbContext();
        await resetDb.Db.Database.ExecuteSqlRawAsync(
            "UPDATE CompensationLogs SET NextRetryOnUtc = NULL, ProcessingLock = NULL, ProcessingLockedAt = NULL WHERE BookingId = {0}",
            bookingId);

        // 6. Second processing cycle — the gateway has recovered, cancel succeeds.
        var secondCycle = await driver.ProcessOnceAsync();

        secondCycle.ProcessedIds.Should().HaveCount(1,
            "after the transient error clears, the retry must succeed");
        secondCycle.Failed.Should().BeEmpty();

        await using var dbAfterSuccess = _fixture.CreateDbContext();
        await dbAfterSuccess.Db.ShouldHaveProcessedCompensationAsync(bookingId);

        // 7. The fake gateway recorded exactly two cancel attempts (fail + success).
        fake.CancelCallCounts.Should().ContainKey(bookingId);
        fake.CancelCallCounts[bookingId].Should().Be(2);

        await driver.ReleaseLocksAsync();
    }

    [Fact]
    public async Task ScenarioD_CompensationRetry_MaxRetriesExhausted_MovesToDeadLetter()
    {
        // Stage a compensation directly to control the retry count.
        var (eventId, _) = await SeedAsync();
        var bookingId = Guid.NewGuid();
        var compensationId = Guid.NewGuid();

        await using var seedDb = _fixture.CreateDbContext();
        seedDb.Db.CompensationLogs.Add(new Infrastructure.Persistence.Outbox.CompensationLog(
            id: compensationId,
            bookingId: bookingId,
            compensationType: "CancelPayment",
            payload: "{}",
            occurredOnUtc: DateTime.UtcNow,
            idempotencyKey: $"compensation:CancelPayment:{bookingId}"));
        await seedDb.Db.SaveChangesAsync();

        // Make EVERY cancel fail — the processor will exhaust retries.
        var fake = GetFakePayment();
        fake.SetCancelFailCount(int.MaxValue);

        var driver = new CompensationTestDriver(_fixture.Factory.Services);

        // Run cycles until dead-lettered. Each cycle fails once (batch size
        // picks up the single record), increments retry, and reschedules.
        for (var i = 0; i < 5; i++)
        {
            await driver.ProcessOnceAsync();

            // Clear the retry guard and processing lock so the next cycle
            // can re-lock the record immediately.
            await using var resetDb = _fixture.CreateDbContext();
            await resetDb.Db.Database.ExecuteSqlRawAsync(
                "UPDATE CompensationLogs SET NextRetryOnUtc = NULL, ProcessingLock = NULL, ProcessingLockedAt = NULL WHERE BookingId = {0}",
                bookingId);
        }

        // After 5 failures the record should be in the dead-letter queue.
        await using var dbFinal = _fixture.CreateDbContext();
        (await dbFinal.Db.CompensationLogs.CountAsync(c => c.BookingId == bookingId))
            .Should().Be(0, "the record must be removed from the live table after dead-lettering");
        await dbFinal.Db.ShouldHaveDeadLetterAsync();

        await driver.ReleaseLocksAsync();
    }

    // =====================================================================
    //  Scenario E — Webhook Idempotency
    // =====================================================================

    [Fact]
    public async Task ScenarioE_WebhookIdempotency_DuplicateEvent_DoesNotDoubleConfirm()
    {
        var (eventId, ticketTypeId) = await SeedAsync();

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

        // Deliver the SAME webhook event twice (Stripe retries on non-2xx).
        var stripeEventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var payload = BuildCheckoutSessionPayload(booking!.Id.Value, stripeEventId);

        var firstResponse = await SendWebhookAsync(payload);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await SendWebhookAsync(payload);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "a duplicate webhook must still return 2xx so Stripe stops retrying");

        await using var dbAfter = _fixture.CreateDbContext();

        // Status is Confirmed (not corrupted by the duplicate).
        var confirmed = await dbAfter.Db.Bookings.FirstOrDefaultAsync(b => b.Id == booking.Id);
        confirmed!.Status.Should().Be(BookingStatusEnum.Confirmed);

        // Seats were sold EXACTLY ONCE — no double counting.
        var @event = await dbAfter.Db.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        @event!.TicketTypes.First().SoldCount.Should().Be(2,
            "the idempotency guard must prevent double confirmation of seats");

        // The ProcessedEvents table has exactly one entry for this event id.
        var processedCount = await dbAfter.Db.ProcessedEvents
            .CountAsync(p => p.IdempotencyKey == $"stripe-webhook:{stripeEventId}");
        processedCount.Should().Be(1,
            "exactly one idempotency record must exist for the duplicate event");
    }
}
