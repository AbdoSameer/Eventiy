using Application.Abstractions.Inventory;
using Application.Features.Bookings.Inventory;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.Inventory;

public class OptimisticReservationStrategyTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Address DefaultAddress = Address.Create(
        "Egypt", "Cairo", "Nile City", "11511", latitude: 30.0444, longitude: 31.2357).Value;

    private static (Event Event, TicketTypeId TicketTypeId) CreatePublishedEventWithTickets(
        int eventCapacity, int ticketCapacity)
    {
        var @event = Event.Create(
            "Test Concert", eventCapacity, UtcNow.AddDays(30),
            DefaultAddress, "Description", EventType.Music, UtcNow).Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, ticketCapacity, UtcNow);
        @event.Publish(UtcNow);
        return (@event, @event.TicketTypes.First().Id);
    }

    [Fact]
    public async Task ReserveAsync_WithAvailableSeats_ShouldSucceedAndMutateAggregate()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 50);
        var strategy = new OptimisticReservationStrategy();
        var context = new ReservationContext(@event, ticketTypeId, 5, UtcNow);

        var result = await strategy.ReserveAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        @event.TicketTypes.First().ReservedCount.Should().Be(5,
            "the optimistic strategy mutates the aggregate in memory so CommitAsync applies the RowVersion check");
    }

    [Fact]
    public async Task ReserveAsync_WhenInsufficientSeats_ShouldFail()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 5);
        var strategy = new OptimisticReservationStrategy();
        var context = new ReservationContext(@event, ticketTypeId, 10, UtcNow);

        var result = await strategy.ReserveAsync(context, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        @event.TicketTypes.First().ReservedCount.Should().Be(0,
            "a failed reservation must not mutate the aggregate");
    }

    [Fact]
    public async Task ReserveAsync_WithUnknownTicketType_ShouldFail()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 50);
        var strategy = new OptimisticReservationStrategy();
        var unknownId = TicketTypeId.Create(Guid.NewGuid()).Value;
        var context = new ReservationContext(@event, unknownId, 1, UtcNow);

        var result = await strategy.ReserveAsync(context, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReserveAsync_OnSuccess_ShouldRaiseTicketTypeSeatsReservedEvent()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 50);
        var strategy = new OptimisticReservationStrategy();
        var context = new ReservationContext(@event, ticketTypeId, 2, UtcNow);

        await strategy.ReserveAsync(context, CancellationToken.None);

        @event.DomainEvents.Should().ContainSingle(e =>
            e.Name == "TicketTypeSeatsReservedEvent");
    }
}
