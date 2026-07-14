using System.Net;
using System.Net.Http.Json;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Scale-level concurrency tests proving the core invariant:
/// successes ≤ capacity under maximum load.
/// </summary>
[Collection("Concurrency")]
public class InventoryScaleTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public InventoryScaleTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(50, 100)]
    [InlineData(10, 1000)]
    public async Task LimitedInventory_NUsersCompeting_ShouldNotExceedCapacity(
        int capacity, int userCount)
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: capacity);

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(userCount, async (int workerIndex) =>
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
            var uniqueUserId = Guid.Parse($"33333333-3333-3333-3333-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().BeLessOrEqualTo(capacity,
            "successes must never exceed capacity — inventory corruption detected if more succeed");

        result.SuccessCount.Should().BeGreaterThan(0,
            "at least some bookings should succeed when capacity > 0");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().BeLessOrEqualTo(capacity,
            "database bookings must never exceed capacity");
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(1, 1000)]
    public async Task LastTicket_AtScale_ShouldAllowExactlyOneBooking(
        int capacity, int userCount)
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: capacity);

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(userCount, async (int workerIndex) =>
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
            var uniqueUserId = Guid.Parse($"44444444-4444-4444-4444-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().Be(1,
            "exactly 1 booking for 1 ticket regardless of user count");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1, "database is the source of truth");
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(1, 100)]
    public async Task MixedPaymentMethod_AtScale_ShouldAllowExactlyOneBooking(
        int capacity, int userCount)
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: capacity);

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(userCount, async (int workerIndex) =>
        {
            var paymentMethod = workerIndex % 2 == 0 ? 0 : 1;
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/booking")
            {
                Content = JsonContent.Create(new
                {
                    EventId = eventId,
                    TicketTypeId = ticketTypeId,
                    Quantity = 1,
                    PaymentMethod = paymentMethod,
                })
            };
            var uniqueUserId = Guid.Parse($"55555555-5555-5555-5555-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().Be(1,
            "exactly 1 booking must succeed regardless of payment method mix");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1, "database is the source of truth");
    }
}
