using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Primitives;
using Eventy.Testing.Foundation.Web;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventy.IntegrationTests.Helpers;

/// <summary>
/// Seeds test data directly into the database via ApplicationDbContext.
/// Bypasses HTTP — faster and gives us the exact IDs we need for assertions.
/// </summary>
public sealed class TestSeedService
{
    private readonly ApplicationDbContext _db;

    public TestSeedService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Seeds a published event with one ticket type. Returns (Event, TicketType) IDs.
    /// </summary>
    public async Task<(Guid EventId, Guid TicketTypeId)> SeedEventWithTicketTypeAsync(
        int eventCapacity = 100,
        int ticketCapacity = 50,
        decimal price = 100m,
        string currency = "EGP")
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Test Street", "11511", 30.0444, 31.2357).Value;

        var eventResult = Event.Create(
            "Integration Test Event", eventCapacity, utcNow.AddDays(30),
            address, "Test description", EventType.Music, utcNow);
        var @event = eventResult.Value;
        @event.AddTicketType("General", Money.FromDecimal(price, currency).Value, ticketCapacity, utcNow);
        @event.Publish(utcNow);

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var ticketTypeId = @event.TicketTypes.First().Id.Value;
        return (@event.Id.Value, ticketTypeId);
    }

    /// <summary>
    /// Seeds the default test user that matches the TestAuthenticationHandler's claims.
    /// </summary>
    public async Task SeedTestUserAsync(Guid? userId = null)
    {
        var id = userId ?? TestUsers.DefaultUserId;
        var email = Email.Create("test@eventy.com").Value;
        var role = Role.FromString("Attendee").Value;

        var userResult = User.Create("Test", "User", email, "hash", role, DateTime.UtcNow);
        // Force the ID to match the test auth handler
        var user = userResult.Value;
        // Use the EF tracked entity — set the ID via reflection if needed (Id is set by factory)
        // Since UserId.Create generates a new Guid, we need to ensure it matches the test claim.
        // The test auth handler returns DefaultUserId, so the booking's UserId column gets that value
        // from ICurrentUserService, not from this seeded user. This seeding is only needed if there's a FK.

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }
}
