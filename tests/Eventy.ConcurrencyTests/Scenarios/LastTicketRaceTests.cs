using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Core concurrency scenario: N users competing for the last ticket.
/// Exactly 1 must succeed — all others must fail with a conflict or capacity error.
/// This is the most dangerous bug class in a ticketing platform:
///   Two valid requests + Same resource + Same millisecond = Inventory corruption
/// </summary>
[Collection("Concurrency")]
public class LastTicketRaceTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public LastTicketRaceTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task LastTicket_With100Users_ShouldAllowExactlyOneBooking()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 1);

        var executor = new ConcurrentExecutor();

        var result = await executor.ExecuteAsync(5, async (int workerIndex) =>
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
            var uniqueUserId = Guid.Parse($"11111111-1111-1111-1111-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().Be(1,
            "because only 1 booking can exist for a single ticket — inventory corruption detected if more succeed");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1, "because database is the source of truth");
    }

    [Fact]
    public async Task LastTicket_MixedInstantDeferred_ShouldAllowExactlyOneBooking()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 1);

        var executor = new ConcurrentExecutor();

        var result = await executor.ExecuteAsync(6, async (int workerIndex) =>
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
            var uniqueUserId = Guid.Parse($"22222222-2222-2222-2222-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().Be(1,
            "exactly one booking must succeed regardless of payment method");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1, "because database is the source of truth");
    }
}
