using Application.Abstractions.Inventory;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using Infrastructure.Inventory;

namespace Eventy.Application.UnitTests.Features.Bookings.Inventory;

public class AtomicRedisReservationStrategyTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Address DefaultAddress = Address.Create(
        "Egypt", "Cairo", "Nile City", "11511", latitude: 30.0444, longitude: 31.2357).Value;

    private static (Event Event, TicketTypeId TicketTypeId) CreateHighDemandEvent(
        int eventCapacity, int ticketCapacity)
    {
        var @event = Event.Create(
            "Flash Sale", eventCapacity, UtcNow.AddDays(30),
            DefaultAddress, "Description", EventType.Music, UtcNow).Value;
        @event.AddTicketType("VIP", Money.FromDecimal(50m, "EGP").Value, ticketCapacity, UtcNow);
        @event.Publish(UtcNow);
        @event.SetHighDemandMode(true, UtcNow);
        return (@event, @event.TicketTypes.First().Id);
    }

    private static AtomicRedisReservationStrategy CreateStrategy(Func<Task<long>> decrementResult)
    {
        var db = Substitute.For<IDatabase>();
        db.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        db.StringDecrementAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<long>(),
            Arg.Any<CommandFlags>())
            .Returns(decrementResult());

        db.StringIncrementAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<long>(),
            Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(0L));

        var logger = Substitute.For<ILogger<AtomicRedisReservationStrategy>>();
        return new AtomicRedisReservationStrategy(() => db, logger);
    }

    [Fact]
    public async Task ReserveAsync_WhenRedisReturnsNonNegative_ShouldSucceed()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 50);
        var strategy = CreateStrategy(() => Task.FromResult(48L));

        var result = await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RedisRemainingCount.Should().Be(48L);
    }

    [Fact]
    public async Task ReserveAsync_OnSuccess_ShouldRaiseRedisSyncEvent()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 50);
        var strategy = CreateStrategy(() => Task.FromResult(48L));

        await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        @event.DomainEvents.Should().ContainSingle(e =>
            e.Name == "TicketTypeRedisReservationSyncedEvent");
    }

    [Fact]
    public async Task ReserveAsync_OnSuccess_ShouldNotMutateInMemoryReservedCount()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 50);
        var reservedBefore = @event.TicketTypes.First().ReservedCount;
        var strategy = CreateStrategy(() => Task.FromResult(48L));

        await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        @event.TicketTypes.First().ReservedCount.Should().Be(reservedBefore,
            "Redis is the source of truth while high-demand mode is on");
    }

    [Fact]
    public async Task ReserveAsync_WhenRedisReturnsNegative_ShouldRollbackAndFail()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 5);
        var strategy = CreateStrategy(() => Task.FromResult(-1L));

        var result = await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Event.RedisInventoryShortfall");
    }

    [Fact]
    public async Task ReserveAsync_WhenSoldOut_ShouldNotRaiseSyncEvent()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 5);
        var strategy = CreateStrategy(() => Task.FromResult(-1L));

        await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        @event.DomainEvents.Should().NotContain(e =>
            e.Name == "TicketTypeRedisReservationSyncedEvent");
    }

    [Fact]
    public async Task ReserveAsync_WhenRedisConnectionFails_ShouldReturnControlledFailure()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 50);

        var connectionEx = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Redis is down");

        // The factory itself throws — simulates Redis being completely
        // unreachable before any command can be issued. The strategy's
        // catch block surfaces this as RedisInventoryUnavailable.
        var logger = Substitute.For<ILogger<AtomicRedisReservationStrategy>>();
        var strategy = new AtomicRedisReservationStrategy(
            () => throw connectionEx,
            logger);

        var result = await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 2, UtcNow),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Event.RedisInventoryUnavailable");
    }

    [Fact]
    public async Task ReserveAsync_WithUnknownTicketType_ShouldFailWithoutRedisCall()
    {
        var (@event, _) = CreateHighDemandEvent(100, 50);
        var db = Substitute.For<IDatabase>();
        var logger = Substitute.For<ILogger<AtomicRedisReservationStrategy>>();
        var strategy = new AtomicRedisReservationStrategy(() => db, logger);
        var unknownId = TicketTypeId.Create(Guid.NewGuid()).Value;

        var result = await strategy.ReserveAsync(
            new ReservationContext(@event, unknownId, 1, UtcNow),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await db.DidNotReceive().StringDecrementAsync(
            Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ReserveAsync_WithZeroQuantity_ShouldFail()
    {
        var (@event, ticketTypeId) = CreateHighDemandEvent(100, 50);
        var strategy = CreateStrategy(() => Task.FromResult(50L));

        var result = await strategy.ReserveAsync(
            new ReservationContext(@event, ticketTypeId, 0, UtcNow),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
