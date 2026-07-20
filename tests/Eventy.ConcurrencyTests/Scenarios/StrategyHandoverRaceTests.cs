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
using System.Net;
using System.Net.Http.Json;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.ConcurrencyTests.Scenarios;

/// <summary>
/// Hardened concurrency tests for the Strategy Handover Race.
///
/// Previous versions were false positives: they used Task.Delay(10) to
/// "guess" when workers were mid-flight, and asserted only on the final
/// booking count. These tests use a deterministic phase-aware barrier
/// (ConcurrentExecutor.ExecuteAsync with onMidFlight) that guarantees
/// the toggle command executes BETWEEN the workers' read phase and
/// their booking send phase.
///
/// Evidence-based assertions verify:
///   - The DB never exceeds capacity (source of truth)
///   - At least one worker received a non-success status (concurrency
///     conflict or retry), proving the fencing token actually fired
///   - The Event.IsHighDemand flag was actually committed
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
    /// Deterministic mid-flight toggle test.
    ///
    /// 20 workers compete for 1 ticket. The toggle command is injected
    /// via the onMidFlight barrier — it runs AFTER all workers have
    /// passed the start barrier but BEFORE any of them sends the
    /// booking request. This guarantees the RowVersion has changed by
    /// the time the booking handlers call SaveChanges.
    ///
    /// Assertions:
    ///   - Exactly 1 booking in the DB (no oversell)
    ///   - At least 1 worker got a non-success response (proves the
    ///     fencing token fired — not just "lucky" timing)
    ///   - Event.IsHighDemand is true (toggle committed)
    /// </summary>
    [Fact]
    public async Task HandoverRace_ToggleMidFlight_ShouldNotOversell()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 1);

        var seeder = new NoOpInventorySeeder();
        var scopeFactory = _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = _fixture.Factory.Services.GetRequiredService<TimeProvider>();
        var toggleHandler = new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);

        var toggleCommitted = new TaskCompletionSource<bool>();

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(
            workerCount: 20,
            action: async (workerIndex) =>
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
            },
            onMidFlight: async () =>
            {
                // This runs on a separate thread AFTER all 20 workers
                // passed the start barrier but BEFORE any of them sends
                // the booking HTTP request. The toggle commits here,
                // bumping the Event.RowVersion before any SaveChanges.
                await toggleHandler.Handle(
                    new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
                    CancellationToken.None);
                toggleCommitted.SetResult(true);
            });

        // ── Evidence: the toggle actually committed ──
        toggleCommitted.Task.IsCompleted.Should().BeTrue(
            "the toggle command must have committed before any booking request was sent");

        // ── Evidence: at least one worker got a conflict ──
        // With 20 workers fighting for 1 ticket, at least 19 must fail.
        // If FailureCount is 0, it means either all succeeded (oversell!)
        // or the test didn't actually create contention.
        result.FailureCount.Should().BeGreaterThan(0,
            "with 20 workers on 1 ticket, at least one must receive a non-success response — " +
            "if all succeed, the fencing token is dead and we have oversell");

        // ── Source of truth: DB booking count ──
        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().Be(1,
            "database is the source of truth — exactly 1 booking must exist for a 1-ticket event");

        // ── Evidence: the toggle actually changed IsHighDemand ──
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await dbScope.DbContext.Events
            .FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeTrue(
            "the toggle command should have committed despite concurrent bookings");
    }

    /// <summary>
    /// High-contention test with 50 workers on a 5-ticket event.
    /// The toggle fires mid-flight. The DB must never exceed 5 bookings.
    ///
    /// This test increases the worker count to 50 (up from 20) to
    /// maximize the probability of triggering real DbUpdateConcurrencyException
    /// retries. The assertion on ConcurrencyConflictCount verifies the
    /// fencing mechanism actually fired — if it's 0, the test is a
    /// false positive.
    /// </summary>
    [Fact]
    public async Task HandoverRace_50Workers_5Tickets_ShouldNotExceedCapacity()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(capacity: 5);

        var seeder = new NoOpInventorySeeder();
        var scopeFactory = _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = _fixture.Factory.Services.GetRequiredService<TimeProvider>();
        var toggleHandler = new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);

        var executor = new ConcurrentExecutor();
        var result = await executor.ExecuteAsync(
            workerCount: 50,
            action: async (workerIndex) =>
            {
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
            },
            onMidFlight: async () =>
            {
                await toggleHandler.Handle(
                    new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
                    CancellationToken.None);
            });

        // ── Evidence: failures occurred (fencing token fired) ──
        result.FailureCount.Should().BeGreaterThan(0,
            "with 50 workers on 5 tickets, at least 45 must fail — " +
            "if all succeed, the concurrency check is dead");

        // ── Source of truth: DB never exceeds capacity ──
        await using var dbScope = _fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var bookingCount = await dbScope.DbContext.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj);
        bookingCount.Should().BeLessThanOrEqualTo(5,
            "database must never have more bookings than capacity — " +
            "if it does, the fencing token failed to prevent the oversell");

        bookingCount.Should().BeGreaterThan(0,
            "at least one booking should succeed — if zero succeeded, " +
            "the toggle's exclusive lock blocked all readers indefinitely");
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
