using Application.Abstractions.Outbox;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventy.IntegrationTests.Assertions;

/// <summary>
/// Extension methods for asserting outbox table state in integration tests.
/// Verifies the Transactional Outbox pattern: every committed transaction
/// must produce exactly one outbox message, and processing must be idempotent.
/// </summary>
public static class OutboxAssertions
{
    /// <summary>
    /// Asserts that exactly <paramref name="expectedCount"/> unprocessed outbox messages
    /// exist for the given <paramref name="eventName"/>.
    /// Unprocessed means ProcessedOnUtc IS NULL (not yet picked up by the processor).
    /// </summary>
    public static async Task ShouldHaveUnprocessedMessagesAsync(
        this ApplicationDbContext db,
        string eventName,
        int expectedCount,
        string because = "",
        string becauseArgs = "")
    {
        var count = await db.OutboxMessages
            .Where(m => m.EventName == eventName && m.ProcessedOnUtc == null)
            .CountAsync();

        count.Should().Be(expectedCount,
            $"because exactly {{0}} unprocessed '{eventName}' outbox message(s) expected. {because}",
            expectedCount, becauseArgs);
    }

    /// <summary>
    /// Asserts that exactly <paramref name="expectedCount"/> outbox messages
    /// have been successfully processed (ProcessedOnUtc IS NOT NULL).
    /// </summary>
    public static async Task ShouldHaveProcessedMessagesAsync(
        this ApplicationDbContext db,
        string eventName,
        int expectedCount)
    {
        var count = await db.OutboxMessages
            .Where(m => m.EventName == eventName && m.ProcessedOnUtc != null)
            .CountAsync();

        count.Should().Be(expectedCount,
            "because exactly {0} processed '{1}' outbox message(s) expected",
            expectedCount, eventName);
    }

    /// <summary>
    /// Asserts that NO unprocessed messages remain for the given event type.
    /// </summary>
    public static async Task ShouldHaveZeroUnprocessedAsync(
        this ApplicationDbContext db,
        string eventName)
    {
        await db.ShouldHaveUnprocessedMessagesAsync(eventName, expectedCount: 0);
    }

    /// <summary>
    /// Asserts that zero dead-letter messages exist (no failed-out messages).
    /// </summary>
    public static async Task ShouldHaveZeroDeadLettersAsync(
        this ApplicationDbContext db)
    {
        var count = await db.OutboxDeadLetters.CountAsync();
        count.Should().Be(0, "because no messages should have reached the dead-letter queue");
    }

    /// <summary>
    /// Asserts that every successful booking has a corresponding outbox message.
    /// This is the "Atomic Commitment" invariant: if the transaction committed,
    /// the outbox row MUST exist in the same transaction.
    /// </summary>
    public static async Task ShouldHaveOutboxForEveryBookingAsync(
        this ApplicationDbContext db,
        int bookingCount)
    {
        var outboxCount = await db.OutboxMessages
            .Where(m => m.EventName == "BookingCreatedEvent")
            .CountAsync();

        outboxCount.Should().Be(bookingCount,
            "because every booking commit must atomically produce one outbox message — " +
            "a mismatch means the transactional outbox pattern is broken");
    }
}
