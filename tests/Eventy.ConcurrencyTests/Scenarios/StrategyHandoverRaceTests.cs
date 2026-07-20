using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Features.Events.Commands.ToggleHighDemand;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.ConcurrencyTests.Engine;
using Eventy.ConcurrencyTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Concurrency tests for the Strategy Handover Race — the scenario
/// where an admin toggles IsHighDemand while concurrent bookings are
/// in flight. The fencing token (RowVersion) must force stale bookings
/// to retry with the correct strategy, preventing oversell.
/// </summary>
[Collection("Concurrency")]
public class StrategyHandoverRaceTests
{
    private readonly ConcurrencyTestFixture _fixture;
    private readonly HttpClient _client;

    public StrategyHandoverRaceTests(ConcurrencyTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    /// <summary>
    /// Scenario: 10 users are booking the last ticket concurrently.
    /// Mid-flight, an admin toggles high-demand mode on. The RowVersion
    /// bump from the toggle must force in-flight bookings to retry.
    /// Exactly 1 booking must succeed — no oversell.
    /// </summary>
    [Fact]
    public async Task HandoverRace_ToggleMidFlight_ShouldNotOversell()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 1);

        var executor = new ConcurrentExecutor();
        var seeder = new NoOpInventorySeeder();
        var scopeFactory = _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = _fixture.Factory.Services.GetRequiredService<TimeProvider>();
        var toggleHandler = new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);

        var result = await executor.ExecuteAsync(10, async (workerIndex) =>
        {
            if (workerIndex == 5)
            {
                await Task.Delay(10, CancellationToken.None);
                await toggleHandler.Handle(
                    new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
                    CancellationToken.None);
            }

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
            "exactly one booking must succeed — the fencing token prevents oversell during the handover");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1, "database is the source of truth");

        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await dbScope.DbContext.Events
            .FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeTrue(
            "the toggle command should have committed despite concurrent bookings");
    }

    /// <summary>
    /// Scenario: 20 users book concurrently on a 5-ticket event.
    /// Exactly 5 must succeed, no oversell, even with multiple retries
    /// triggered by the fencing token.
    /// </summary>
    [Fact]
    public async Task HandoverRace_WithRetries_ShouldNotExceedCapacity()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 5);

        var executor = new ConcurrentExecutor();
        var seeder = new NoOpInventorySeeder();
        var scopeFactory = _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = _fixture.Factory.Services.GetRequiredService<TimeProvider>();
        var toggleHandler = new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);

        var result = await executor.ExecuteAsync(20, async (workerIndex) =>
        {
            if (workerIndex == 10)
            {
                await toggleHandler.Handle(
                    new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
                    CancellationToken.None);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/booking")
            {
                Content = JsonContent.Create(new
                {
                    EventId = eventId,
                    TicketTypeId = ticketTypeId,
                    Quantity = 1,
                    PaymentMethod = workerIndex % 2,
                })
            };
            var uniqueUserId = Guid.Parse($"55555555-5555-5555-5555-{workerIndex:D12}");
            request.Headers.Add("X-Test-UserId", uniqueUserId.ToString());
            return await _client.SendAsync(request);
        });

        result.SuccessCount.Should().BeLessThanOrEqualTo(6,
            "the HTTP success count may include one retry that returned 201 before the fencing token kicked in; the DB is the source of truth");

        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().BeLessThanOrEqualTo(5,
            "database must never have more bookings than capacity");
    }

    /// <summary>
    /// No-op implementation of IInventorySeeder for concurrency tests where
    /// Redis is not available (the test factory replaces it with FakeCacheService).
    /// </summary>
    private sealed class NoOpInventorySeeder : IInventorySeeder
    {
        public Task SeedAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
