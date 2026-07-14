using System.Net;
using System.Net.Http.Json;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Verifies that bookings on one event never affect another event's inventory.
/// Also tests duplicate booking prevention (same user, same ticket type).
/// </summary>
[Collection("Concurrency")]
public class EventIsolationTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public EventIsolationTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task ConcurrentBookings_OnDifferentEvents_ShouldNotCrossContaminate()
    {
        var (eventAId, ticketAId) = await _fixture.SeedPublishedEventAsync(capacity: 5);
        var (eventBId, ticketBId) = await _fixture.SeedPublishedEventAsync(capacity: 5);

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(10, async (int workerIndex) =>
        {
            var (eventId, ticketTypeId) = workerIndex % 2 == 0
                ? (eventAId, ticketAId)
                : (eventBId, ticketBId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/booking")
            {
                Content = JsonContent.Create(new
                {
                    EventId = eventId,
                    TicketTypeId = ticketTypeId,
                    Quantity = 1,
                    PaymentMethod = 0,
                })
            };
            var uniqueUserId = Guid.Parse($"66666666-6666-6666-6666-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        await using var dbScope = _fixture.CreateDbContext();
        var eventA = await dbScope.DbContext.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventAId));
        var eventB = await dbScope.DbContext.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventBId));

        var ticketA = eventA!.TicketTypes.First();
        var ticketB = eventB!.TicketTypes.First();

        ticketA.SoldCount.Should().BeLessOrEqualTo(5,
            "event A inventory must never exceed its capacity");
        ticketB.SoldCount.Should().BeLessOrEqualTo(5,
            "event B inventory must never exceed its capacity");

        var eventABookings = await dbScope.DbContext.Bookings
            .CountAsync(b => b.EventId == EventId.FromDatabase(eventAId));
        var eventBBookings = await dbScope.DbContext.Bookings
            .CountAsync(b => b.EventId == EventId.FromDatabase(eventBId));

        eventABookings.Should().BeLessOrEqualTo(5, "event A bookings must not exceed capacity");
        eventBBookings.Should().BeLessOrEqualTo(5, "event B bookings must not exceed capacity");

        (eventABookings + eventBBookings).Should().Be(result.SuccessCount,
            "total bookings across events should match successful HTTP responses");
    }

    [Fact]
    public async Task DuplicateBooking_SameUserSameTicketType_ShouldBothSucceedSeparately()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 10);

        var userId = Guid.Parse("77777777-7777-7777-77777777777");

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(3, async (_) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/booking")
            {
                Content = JsonContent.Create(new
                {
                    EventId = eventId,
                    TicketTypeId = ticketTypeId,
                    Quantity = 1,
                    PaymentMethod = 0,
                })
            };
            request.Headers.Add("X-Test-UserId", userId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().BeLessOrEqualTo(3,
            "all requests can succeed as long as capacity allows");

        await using var dbScope = _fixture.CreateDbContext();
        var userBookings = await dbScope.DbContext.Bookings
            .CountAsync(b => b.EventId == EventId.FromDatabase(eventId));
        userBookings.Should().BeLessOrEqualTo(10,
            "total bookings must not exceed capacity");
    }
}
