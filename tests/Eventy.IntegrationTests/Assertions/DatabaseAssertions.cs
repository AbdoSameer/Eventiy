using Domain.Aggregates.BookingAggregate.ValueObject;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventy.IntegrationTests.Assertions;

/// <summary>
/// Extension methods for asserting database state in integration tests.
/// HTTP responses can lie — always verify database reality.
/// </summary>
public static class DatabaseAssertions
{
    /// <summary>
    /// Counts bookings for a ticket type. Compares against the value object
    /// (EF Core applies its configured converter to extract the Guid automatically).
    /// </summary>
    public static async Task ShouldHaveBookingCountAsync(
        this ApplicationDbContext db,
        Guid ticketTypeId,
        int expectedCount)
    {
        var typedId = Domain.Aggregates.EventAggregate.ValueObject.TicketTypeId.FromDatabase(ticketTypeId);
        var count = await db.Bookings
            .CountAsync(b => b.TicketTypeId == typedId);
        count.Should().Be(expectedCount,
            "because exactly {0} booking(s) should exist for ticket type {1}", expectedCount, ticketTypeId);
    }

    public static async Task ShouldHaveOutboxMessageAsync(
        this ApplicationDbContext db,
        string eventType)
    {
        var exists = await db.OutboxMessages
            .AnyAsync(o => o.EventName == eventType);
        exists.Should().BeTrue("because an outbox message of type {0} should have been persisted", eventType);
    }

    public static async Task ShouldHaveEventAsync(
        this ApplicationDbContext db,
        Guid eventId)
    {
        var typedId = Domain.Aggregates.EventAggregate.ValueObject.EventId.FromDatabase(eventId);
        var exists = await db.Events
            .AnyAsync(e => e.Id == typedId);
        exists.Should().BeTrue("because the event {0} should exist in the database", eventId);
    }
}
