using Eventy.IntegrationTests.Fixtures;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Eventy.IntegrationTests;

/// <summary>
/// Rule 3: Logging the State — provides Before/After state logging
/// for integration tests. Logs SQL inventory counts, booking counts,
/// and compensation log states to the xUnit test output for debugging
/// flaky tests.
///
/// Usage:
/// <code>
/// await State.LogAsync("Before reconciliation", eventId, ticketTypeId);
/// // ... act ...
/// await State.LogAsync("After reconciliation", eventId, ticketTypeId);
/// </code>
///
/// The output appears in the xUnit test runner and CI logs, making
/// it easy to trace state transitions during test failures.
/// </summary>
public sealed class TestStateLogger
{
    private readonly ITestOutputHelper _output;
    private readonly IntegrationTestFixture _fixture;

    public TestStateLogger(ITestOutputHelper output, IntegrationTestFixture fixture)
    {
        _output = output;
        _fixture = fixture;
    }

    public void LogTestStart(string testName)
    {
        _output.WriteLine($"┌─────────────────────────────────────────────────────────────");
        _output.WriteLine($"│ TEST START: {testName}");
        _output.WriteLine($"├─────────────────────────────────────────────────────────────");
    }

    public void LogTestEnd(string testName)
    {
        _output.WriteLine($"├─────────────────────────────────────────────────────────────");
        _output.WriteLine($"│ TEST END: {testName}");
        _output.WriteLine($"└─────────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Logs the current state of an event and its ticket types.
    /// Shows SQL inventory (Capacity, SoldCount, ReservedCount, AvailableCount),
    /// IsHighDemand flag, and the number of bookings per ticket type.
    /// </summary>
    public async Task LogAsync(string label, Guid eventId, Guid? ticketTypeId = null)
    {
        await using var db = _fixture.CreateDbContext();
        var eventIdObj = Domain.Aggregates.EventAggregate.ValueObject.EventId.FromDatabase(eventId);

        var @event = await db.Db.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == eventIdObj);

        if (@event is null)
        {
            _output.WriteLine($"│ [{label}] Event {eventId} — NOT FOUND");
            return;
        }

        _output.WriteLine($"│ [{label}] Event: {@event.EventName.Value}");
        _output.WriteLine($"│   IsHighDemand: {@event.IsHighDemand}, Status: {@event.Status}");

        foreach (var tt in @event.TicketTypes)
        {
            if (ticketTypeId.HasValue && tt.Id.Value != ticketTypeId.Value)
                continue;

            _output.WriteLine($"│   TicketType: {tt.TicketTypeName} ({tt.Id.Value})");
            _output.WriteLine($"│     Capacity={tt.Capacity}, Sold={tt.SoldCount}, Reserved={tt.ReservedCount}, Available={tt.AvailableCount}");

            var bookingCount = await db.Db.Bookings
                .CountAsync(b => b.TicketTypeId == tt.Id);
            var pendingCount = await db.Db.Bookings
                .CountAsync(b => b.TicketTypeId == tt.Id
                    && (b.Status == Domain.Aggregates.BookingAggregate.Enums.BookingStatusEnum.Pending
                        || b.Status == Domain.Aggregates.BookingAggregate.Enums.BookingStatusEnum.PendingPayment));
            _output.WriteLine($"│     Bookings: total={bookingCount}, pending={pendingCount}");
        }
    }

    /// <summary>
    /// Synchronous convenience wrapper for non-async test contexts.
    /// Logs a simple label without DB state.
    /// </summary>
    public void Log(string label, Guid eventId, Guid? ticketTypeId = null)
    {
        _output.WriteLine($"│ [{label}] Event={eventId}, TicketType={ticketTypeId ?? Guid.Empty}");
    }

    /// <summary>
    /// Logs compensation log entries for a given booking.
    /// </summary>
    public async Task LogCompensationAsync(string label, Guid bookingId)
    {
        await using var db = _fixture.CreateDbContext();
        var logs = await db.Db.CompensationLogs
            .Where(c => c.BookingId == bookingId)
            .ToListAsync();

        _output.WriteLine($"│ [{label}] CompensationLogs for booking {bookingId}:");
        if (logs.Count == 0)
        {
            _output.WriteLine($"│   (none)");
            return;
        }
        foreach (var log in logs)
        {
            _output.WriteLine($"│   Type={log.CompensationType}, Processed={log.ProcessedOnUtc.HasValue}, Retry={log.RetryCount}");
        }
    }
}
