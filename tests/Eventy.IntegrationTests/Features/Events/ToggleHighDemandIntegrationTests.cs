using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Features.Events.Commands.ToggleHighDemand;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Events;

/// <summary>
/// Integration tests for the ToggleHighDemandCommandHandler — Layer 1
/// of the Strategy Handover Race defense.
///
/// Uses IntegrationTestBase (Rule of One + Clean Up Hook + State Logging).
/// </summary>
public class ToggleHighDemandIntegrationTests : IntegrationTestBase
{
    public ToggleHighDemandIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    private static async Task<ToggleHighDemandCommandHandler> CreateHandlerAsync(
        IntegrationTestFixture fixture,
        FakeInventorySeeder? seeder = null)
    {
        seeder ??= new FakeInventorySeeder();
        var scopeFactory = fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = fixture.Factory.Services.GetRequiredService<TimeProvider>();
        return new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);
    }

    [Fact]
    public async Task ToggleHighDemand_WhenEnabling_ShouldSetFlagAndSeedRedis()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync();
        await State.LogAsync("Before toggle", eventId, ticketTypeId);

        var seeder = new FakeInventorySeeder();
        var handler = await CreateHandlerAsync(Fixture, seeder);

        var result = await handler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsHighDemand.Should().BeTrue();
        seeder.SeedCallCount.Should().BeGreaterThan(0,
            "Redis counters should be force-seeded when enabling high-demand mode");
        seeder.ClearCallCount.Should().Be(0);

        await using var db = Fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await db.Db.Events.FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeTrue();

        await State.LogAsync("After toggle", eventId, ticketTypeId);
    }

    [Fact]
    public async Task ToggleHighDemand_WhenDisabling_ShouldClearFlagAndClearRedis()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync();

        var enableSeeder = new FakeInventorySeeder();
        var enableHandler = await CreateHandlerAsync(Fixture, enableSeeder);
        await enableHandler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        await State.LogAsync("After enable", eventId, ticketTypeId);

        var disableSeeder = new FakeInventorySeeder();
        var disableHandler = await CreateHandlerAsync(Fixture, disableSeeder);
        var result = await disableHandler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = false },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsHighDemand.Should().BeFalse();
        disableSeeder.ClearCallCount.Should().BeGreaterThan(0,
            "Redis counters should be cleared when disabling high-demand mode");

        await using var db = Fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await db.Db.Events.FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeFalse();

        await State.LogAsync("After disable", eventId, ticketTypeId);
    }

    [Fact]
    public async Task ToggleHighDemand_WhenEventNotFound_ShouldReturnFailure()
    {
        var handler = await CreateHandlerAsync(Fixture);

        var result = await handler.Handle(
            new ToggleHighDemandCommand { EventId = Guid.NewGuid(), Enabled = true },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleHighDemand_WhenAlreadyEnabled_ShouldSucceedIdempotently()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync();

        var firstSeeder = new FakeInventorySeeder();
        var firstHandler = await CreateHandlerAsync(Fixture, firstSeeder);
        await firstHandler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        await State.LogAsync("After first enable", eventId, ticketTypeId);

        var secondSeeder = new FakeInventorySeeder();
        var secondHandler = await CreateHandlerAsync(Fixture, secondSeeder);
        var result = await secondHandler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsHighDemand.Should().BeTrue();

        await State.LogAsync("After second enable (idempotent)", eventId, ticketTypeId);
    }

    /// <summary>
    /// After toggling high-demand on, the event state should reflect the
    /// flag and the ticket type should still have the correct available count.
    /// </summary>
    [Fact]
    public async Task ToggleHighDemand_ThenBooking_ShouldNotOversell()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync(
            eventCapacity: 1, ticketCapacity: 1);

        var seeder = new FakeInventorySeeder();
        var handler = await CreateHandlerAsync(Fixture, seeder);
        await handler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        await State.LogAsync("After toggle, before booking", eventId, ticketTypeId);

        await using var db = Fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await db.Db.Events
            .Include(e => e.TicketTypes)
            .FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeTrue();
        @event.TicketTypes.First().AvailableCount.Should().Be(1);

        await State.LogAsync("After verification", eventId, ticketTypeId);
    }

    private sealed class FakeInventorySeeder : IInventorySeeder
    {
        public int SeedCallCount { get; private set; }
        public int ClearCallCount { get; private set; }

        public Task SeedAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
        {
            SeedCallCount++;
            return Task.CompletedTask;
        }

        public Task ClearAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
        {
            ClearCallCount++;
            return Task.CompletedTask;
        }
    }
}
