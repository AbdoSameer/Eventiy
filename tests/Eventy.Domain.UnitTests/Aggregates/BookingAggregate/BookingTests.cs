using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates.BookingAggregate;

public class BookingTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestEventId = Guid.NewGuid();
    private static readonly Guid TestTicketTypeId = Guid.NewGuid();

    private static Result<Booking> CreateValidBooking(int quantity = 1, PaymentMethod payment = PaymentMethod.Instant)
    {
        return Booking.Create(
            UserId.FromDatabase(TestUserId),
            EventId.FromDatabase(TestEventId),
            TicketTypeId.FromDatabase(TestTicketTypeId),
            "Test Event",
            quantity,
            Money.FromDecimal(100m, "EGP").Value,
            payment,
            UtcNow);
    }

    #region Create

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        var result = CreateValidBooking();

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Value.Should().Be(TestUserId);
        result.Value.Status.Should().Be(BookingStatusEnum.Pending);
        result.Value.Quantity.Should().Be(1);
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldReturnFailure()
    {
        var result = CreateValidBooking(quantity: 0);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNegativeQuantity_ShouldReturnFailure()
    {
        var result = CreateValidBooking(quantity: -1);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Confirm

    [Fact]
    public void Confirm_WhenPending_ShouldTransitionToConfirmed()
    {
        var booking = CreateValidBooking().Value;

        var result = booking.Confirm(UtcNow);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Confirmed);
        booking.ConfirmationDate.Should().BeCloseTo(UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Confirm_WhenAlreadyCancelled_ShouldReturnFailure()
    {
        var booking = CreateValidBooking().Value;
        booking.Cancel(UtcNow);

        var result = booking.Confirm(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_ShouldReturnFailure()
    {
        var booking = CreateValidBooking().Value;
        booking.Confirm(UtcNow);

        var result = booking.Confirm(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        var booking = CreateValidBooking().Value;

        var result = booking.Cancel(UtcNow, "Changed mind");

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Cancelled);
        booking.CancellationReason.Should().Be("Changed mind");
    }

    [Fact]
    public void Cancel_WhenConfirmed_ShouldTransitionToCancelled()
    {
        var booking = CreateValidBooking().Value;
        booking.Confirm(UtcNow);

        var result = booking.Cancel(UtcNow);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldReturnFailure()
    {
        var booking = CreateValidBooking().Value;
        booking.Cancel(UtcNow);

        var result = booking.Cancel(UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Refund

    [Fact]
    public void Refund_WhenCancelled_ShouldTransitionToRefunded()
    {
        var booking = CreateValidBooking().Value;
        booking.Cancel(UtcNow);

        var result = booking.Refund(UtcNow);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Refunded);
    }

    [Fact]
    public void Refund_WhenConfirmed_ShouldReturnFailure()
    {
        var booking = CreateValidBooking().Value;
        booking.Confirm(UtcNow);

        var result = booking.Refund(UtcNow);

        result.IsFailure.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Confirmed);
    }

    #endregion

    #region Expire

    [Fact]
    public void Expire_WhenPendingAndHoldExpired_ShouldTransitionToExpired()
    {
        var booking = CreateValidBooking(payment: PaymentMethod.Deferred);
        var afterHoldExpiry = UtcNow.AddMinutes(35);

        var result = booking.Value.Expire(afterHoldExpiry);

        result.IsSuccess.Should().BeTrue();
        booking.Value.Status.Should().Be(BookingStatusEnum.Expired);
    }

    #endregion

    #region CanBeModified / IsActive

    [Fact]
    public void CanBeModified_WhenPending_ShouldReturnTrue()
    {
        var booking = CreateValidBooking().Value;

        booking.CanBeModified().Should().BeTrue();
    }

    [Fact]
    public void CanBeModified_WhenConfirmed_ShouldReturnFalse()
    {
        var booking = CreateValidBooking().Value;
        booking.Confirm(UtcNow);

        booking.CanBeModified().Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenPending_ShouldReturnTrue()
    {
        var booking = CreateValidBooking().Value;

        booking.IsActive().Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenCancelled_ShouldReturnFalse()
    {
        var booking = CreateValidBooking().Value;
        booking.Cancel(UtcNow);

        booking.IsActive().Should().BeFalse();
    }

    #endregion
}
