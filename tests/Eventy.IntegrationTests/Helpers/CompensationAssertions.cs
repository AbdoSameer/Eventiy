using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventy.IntegrationTests.Helpers;

/// <summary>
/// FluentAssertions extensions for the <c>CompensationLogs</c> and
/// <c>OutboxDeadLetters</c> tables. These verify the Durable Compensation
/// invariants: a failed payment MUST leave a persistent record, and the
/// processor MUST eventually mark it processed (or dead-letter it).
/// </summary>
public static class CompensationAssertions
{
    /// <summary>
    /// Asserts that at least one unprocessed compensation record exists for the
    /// given booking. This is the core durability proof — a crash after this
    /// row exists cannot lose the compensation intent.
    /// </summary>
    public static async Task ShouldHaveCompensationForBookingAsync(
        this ApplicationDbContext db,
        Guid bookingId,
        string compensationType = "CancelPayment")
    {
        var exists = await db.CompensationLogs
            .AnyAsync(c => c.BookingId == bookingId && c.CompensationType == compensationType);

        exists.Should().BeTrue(
            "booking {0} must have a durable {1} compensation record after a payment failure — " +
            "its absence means the compensation was lost on crash",
            bookingId, compensationType);
    }

    /// <summary>Asserts the total compensation-log row count.</summary>
    public static async Task ShouldHaveCompensationCountAsync(
        this ApplicationDbContext db,
        int expectedCount)
    {
        var count = await db.CompensationLogs.CountAsync();
        count.Should().Be(expectedCount,
            "because exactly {0} compensation record(s) were expected", expectedCount);
    }

    /// <summary>
    /// Asserts a compensation record is marked processed (ProcessedOnUtc set).
    /// </summary>
    public static async Task ShouldHaveProcessedCompensationAsync(
        this ApplicationDbContext db,
        Guid bookingId)
    {
        var processed = await db.CompensationLogs
            .AnyAsync(c => c.BookingId == bookingId && c.ProcessedOnUtc != null);

        processed.Should().BeTrue(
            "the compensation for booking {0} should have been processed by the CompensationProcessor",
            bookingId);
    }

    /// <summary>
    /// Asserts a compensation record's retry count matches the expected value.
    /// Used to verify the exponential-backoff retry path.
    /// </summary>
    public static async Task ShouldHaveCompensationRetryCountAsync(
        this ApplicationDbContext db,
        Guid bookingId,
        int expectedRetryCount)
    {
        var log = await db.CompensationLogs
            .FirstOrDefaultAsync(c => c.BookingId == bookingId);

        log.Should().NotBeNull("a compensation record for booking {0} should exist", bookingId);
        log!.RetryCount.Should().Be(expectedRetryCount,
            "the compensation should have been retried {0} time(s)", expectedRetryCount);
    }

    /// <summary>
    /// Asserts that a dead-letter entry exists for the given compensation type.
    /// Verifies the max-retries exhaustion path moves records out of the live
    /// table into the dead-letter queue.
    /// </summary>
    public static async Task ShouldHaveDeadLetterAsync(
        this ApplicationDbContext db,
        string domain = "Compensation")
    {
        var count = await db.OutboxDeadLetters.CountAsync(d => d.Domain == domain);
        count.Should().BeGreaterThan(0,
            "an exhausted compensation should have been moved to the dead-letter queue");
    }

    /// <summary>Asserts NO dead-letter entries exist.</summary>
    public static async Task ShouldHaveZeroCompensationDeadLettersAsync(
        this ApplicationDbContext db)
    {
        var count = await db.OutboxDeadLetters.CountAsync(d => d.Domain == "Compensation");
        count.Should().Be(0, "no compensation should have reached the dead-letter queue");
    }
}
