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
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, ticketCapacity, UtcNow);
        @event.Publish(UtcNow);
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
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);
        @event.Publish(UtcNow);

        var result = @event.Publish(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Publish_WhenCancelled_ShouldReturnFailure()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);
        @event.Publish(UtcNow);
        @event.Cancel(UtcNow);

        var result = @event.Publish(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_WhenDraft_ShouldTransitionToCancelled()
    {
        var @event = CreateValidEvent().Value;

        var result = @event.Cancel(UtcNow, "No longer needed");

        result.IsSuccess.Should().BeTrue();
        @event.Status.Should().Be(EventStatus.Cancelled);
        @event.CancellationReason.Should().Be("No longer needed");
    }

    [Fact]
    public void Cancel_WhenPublished_ShouldReturnFailure()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);
        @event.Publish(UtcNow);

        var result = @event.Cancel(UtcNow, "Low attendance");

        result.IsFailure.Should().BeTrue(
            "a published event cannot be cancelled via Cancel(); use AdminCancel() instead");
    }

    [Fact]
    public void AdminCancel_WhenPublished_ShouldTransitionToCancelled()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);
        @event.Publish(UtcNow);

        var result = @event.AdminCancel(UtcNow, "Low attendance");

        result.IsSuccess.Should().BeTrue();
        @event.Status.Should().Be(EventStatus.Cancelled);
        @event.CancellationReason.Should().Be("Low attendance");
    }

    #endregion

    #region AddTicketType

    [Fact]
    public void AddTicketType_WhenDraft_ShouldSucceed()
    {
        var @event = CreateValidEvent().Value;

        var result = @event.AddTicketType("VIP", Money.FromDecimal(200m, "EGP").Value, 50, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.TicketTypes.Should().Contain(t => t.TicketTypeName == "VIP");
    }

    [Fact]
    public void AddTicketType_WhenPublished_ShouldFail()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 50, UtcNow);
        @event.Publish(UtcNow);

        var result = @event.AddTicketType("VIP", Money.FromDecimal(200m, "EGP").Value, 50, UtcNow);

        result.IsFailure.Should().BeTrue(
            "ticket types cannot be added after an event is published");
    }

    [Fact]
    public void AddTicketType_WhenTotalExceedsEventCapacity_ShouldReturnFailure()
    {
        var @event = CreateValidEvent(capacity: 10).Value;
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

    [Fact]
    public void ReserveSeats_WhenEventIsDraft_ShouldReturnFailure()
    {
        var @event = CreateValidEvent().Value;
        @event.AddTicketType("General", Money.FromDecimal(50m, "EGP").Value, 10, UtcNow);
        var ticketTypeId = @event.TicketTypes.First().Id;

        var result = @event.ReserveSeats(ticketTypeId, 1, UtcNow);

        result.IsFailure.Should().BeTrue(
            "seats cannot be reserved on an unpublished event");
        result.Errors[0].Code.Should().Be("Event.CannotReserveOnUnpublished");
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

    #region HasAvailableSeats

    [Fact]
    public void HasAvailableSeats_WhenSoldOut_ShouldReturnFalse()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(5, 5);
        @event.ReserveSeats(ticketTypeId, 5, UtcNow);
        @event.ConfirmReservation(ticketTypeId, 5, UtcNow);

        @event.HasAvailableSeats().Should().BeFalse();
    }

    #endregion

    #region SetHighDemandMode

    [Fact]
    public void SetHighDemandMode_WhenPublished_ShouldEnableHighDemand()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 50);

        var result = @event.SetHighDemandMode(true, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.IsHighDemand.Should().BeTrue();
    }

    [Fact]
    public void SetHighDemandMode_WhenDraft_ShouldFail()
    {
        var @event = CreateValidEvent().Value;

        var result = @event.SetHighDemandMode(true, UtcNow);

        result.IsFailure.Should().BeTrue();
        @event.IsHighDemand.Should().BeFalse();
    }

    [Fact]
    public void SetHighDemandMode_WhenAlreadyEnabled_ShouldBeIdempotent()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);

        var result = @event.SetHighDemandMode(true, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.IsHighDemand.Should().BeTrue();
    }

    [Fact]
    public void SetHighDemandMode_WhenDisabling_ShouldDisable()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);

        var result = @event.SetHighDemandMode(false, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.IsHighDemand.Should().BeFalse();
    }

    #endregion

    #region RecordRedisReservation

    [Fact]
    public void RecordRedisReservation_WithValidTicketType_ShouldRaiseSyncEvent()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);

        var result = @event.RecordRedisReservation(ticketTypeId, 2, 48, UtcNow);

        result.IsSuccess.Should().BeTrue();
        @event.DomainEvents.Should().ContainSingle(e =>
            e.Name == "TicketTypeRedisReservationSyncedEvent");
    }

    [Fact]
    public void RecordRedisReservation_WithUnknownTicketType_ShouldFail()
    {
        var (@event, _) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);
        var unknownId = TicketTypeId.Create(Guid.NewGuid()).Value;

        var result = @event.RecordRedisReservation(unknownId, 2, 48, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecordRedisReservation_WithZeroQuantity_ShouldFail()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);

        var result = @event.RecordRedisReservation(ticketTypeId, 0, 50, UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The Redis path must NOT mutate the in-memory ReservedCount — Redis is
    /// the source of truth while high-demand mode is on.
    /// </summary>
    [Fact]
    public void RecordRedisReservation_ShouldNotMutateInMemoryReservedCount()
    {
        var (@event, ticketTypeId) = CreatePublishedEventWithTickets(100, 50);
        @event.SetHighDemandMode(true, UtcNow);
        var reservedBefore = @event.TicketTypes.First().ReservedCount;

        @event.RecordRedisReservation(ticketTypeId, 2, 48, UtcNow);

        @event.TicketTypes.First().ReservedCount.Should().Be(reservedBefore,
            "Redis is the source of truth; in-memory count is synced later by the background processor");
    }

    #endregion
}
