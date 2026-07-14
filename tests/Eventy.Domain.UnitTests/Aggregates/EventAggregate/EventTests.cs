using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates.EventAggregate;

public class EventTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Address DefaultAddress = Address.Create(
        "Egypt", "Cairo", "Nile City", "11511", latitude: 30.0444, longitude: 31.2357).Value;

    private static Result<Event> CreateValidEvent(
        int capacity = 100,
        string name = "Test Event",
        EventType type = EventType.Music)
    {
        return Event.Create(name, capacity, UtcNow.AddDays(30), DefaultAddress, "Description", type, UtcNow);
    }

    /// <summary>
    /// Helper: creates a published event with one "General" ticket type of given capacity.
    /// Returns the event and the ticket type ID.
    /// </summary>
    private static (Event Event, TicketTypeId TicketTypeId) CreatePublishedEventWithTickets(int eventCapacity, int ticketCapacity)
    {
        var @event = CreateValidEvent(capacity: eventCapacity).Value;
        @event.Publish(UtcNow);
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, ticketCapacity, UtcNow);
        var ticketTypeId = @event.TicketTypes.First().Id;
        return (@event, ticketTypeId);
    }

    #region Create

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        var result = CreateValidEvent();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public void Create_WithZeroCapacity_ShouldReturnFailure()
    {
        var result = CreateValidEvent(capacity: 0);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        var result = CreateValidEvent(name: "");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithPastDate_ShouldReturnFailure()
    {
        var result = Event.Create(
            "Test Event", 100, UtcNow.AddDays(-1), DefaultAddress, "Desc", EventType.Music, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Publish

    [Fact]
    public void Publish_WhenDraftAndHasTickets_ShouldTransitionToPublished()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);

        var result = @event.Publish(UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.Status.Should().Be(EventStatus.Published);
        @event.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ShouldReturnFailure()
    {
        var @event = CreateValidEvent().Value;
        @event.Publish(UtcNow);

        var result = @event.Publish(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Publish_WhenCancelled_ShouldReturnFailure()
    {
        var @event = CreateValidEvent().Value;
        @event.Publish(UtcNow);
        @event.Cancel(UtcNow);

        var result = @event.Publish(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_WhenPublished_ShouldTransitionToCancelled()
    {
        var @event = CreateValidEvent().Value;
        @event.Publish(UtcNow);

        var result = @event.Cancel(UtcNow, "Low attendance");

        result.IsSuccess.Should().BeTrue();
        @event.Status.Should().Be(EventStatus.Cancelled);
        @event.CancellationReason.Should().Be("Low attendance");
    }

    [Fact]
    public void Cancel_WhenDraft_ShouldTransitionToCancelled()
    {
        var @event = CreateValidEvent().Value;

        var result = @event.Cancel(UtcNow, "No longer needed");

        result.IsSuccess.Should().BeTrue();
        @event.Status.Should().Be(EventStatus.Cancelled);
        @event.CancellationReason.Should().Be("No longer needed");
    }

    #endregion

    #region AddTicketType

    [Fact]
    public void AddTicketType_WhenPublished_ShouldSucceed()
    {
        var @event = CreateValidEvent().Value;
        @event.Publish(UtcNow);

        var result = @event.AddTicketType("VIP", Money.FromDecimal(200m, "EGP").Value, 50, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.TicketTypes.Should().Contain(t => t.TicketTypeName == "VIP");
    }

    [Fact]
    public void AddTicketType_WhenTotalExceedsEventCapacity_ShouldReturnFailure()
    {
        var @event = CreateValidEvent(capacity: 10).Value;
        @event.Publish(UtcNow);
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 10, UtcNow);

        var result = @event.AddTicketType("VIP", Money.FromDecimal(200m, "EGP").Value, 1, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ReserveSeats

    [Fact]
    public void ReserveSeats_WhenAvailable_ShouldIncrementReservedCount()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(10, 10);

        var result = @event.ReserveSeats(ticketTypeId, 3, UtcNow);

        result.IsSuccess.Should().BeTrue();
        var ticket = @event.TicketTypes.First(t => t.Id == ticketTypeId);
        ticket.ReservedCount.Should().Be(3);
    }

    [Fact]
    public void ReserveSeats_WhenExceedsAvailable_ShouldReturnFailure()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(5, 5);

        var result = @event.ReserveSeats(ticketTypeId, 10, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReserveSeats_ExactlyLastAvailable_ShouldSucceed()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(3, 3);
        @event.ReserveSeats(ticketTypeId, 2, UtcNow);

        var result = @event.ReserveSeats(ticketTypeId, 1, UtcNow);

        result.IsSuccess.Should().BeTrue();
        var ticket = @event.TicketTypes.First(t => t.Id == ticketTypeId);
        ticket.ReservedCount.Should().Be(3);
        ticket.AvailableCount.Should().Be(0);
    }

    #endregion

    #region ConfirmReservation / ReleaseSeats

    [Fact]
    public void ConfirmReservation_ShouldDecrementReservedAndIncrementSold()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(10, 10);
        @event.ReserveSeats(ticketTypeId, 3, UtcNow);

        var result = @event.ConfirmReservation(ticketTypeId, 3, UtcNow);

        result.IsSuccess.Should().BeTrue();
        var ticket = @event.TicketTypes.First(t => t.Id == ticketTypeId);
        ticket.ReservedCount.Should().Be(0);
        ticket.SoldCount.Should().Be(3);
    }

    [Fact]
    public void ReleaseSeats_ShouldDecrementReserved()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(10, 10);
        @event.ReserveSeats(ticketTypeId, 5, UtcNow);

        var result = @event.ReleaseSeats(ticketTypeId, 2, UtcNow);

        result.IsSuccess.Should().BeTrue();
        var ticket = @event.TicketTypes.First(t => t.Id == ticketTypeId);
        ticket.ReservedCount.Should().Be(3);
    }

    #endregion

    #region GetRemainingCapacity / HasAvailableSeats

    [Fact]
    public void GetRemainingCapacity_WithNoReservations_ShouldReturnTotalCapacity()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 100);

        @event.GetRemainingCapacity().Should().Be(100);
    }

    [Fact]
    public void HasAvailableSeats_WhenSoldOut_ShouldReturnFalse()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(5, 5);
        @event.ReserveSeats(ticketTypeId, 5, UtcNow);
        @event.ConfirmReservation(ticketTypeId, 5, UtcNow);

        @event.HasAvailableSeats().Should().BeFalse();
    }

    #endregion
}
