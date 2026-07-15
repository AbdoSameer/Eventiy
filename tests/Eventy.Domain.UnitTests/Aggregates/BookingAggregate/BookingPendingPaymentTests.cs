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

/// <summary>
/// Tests for the new PendingPayment status introduced by the Pending-First
/// pattern. PendingPayment is the transitional state after the booking is
/// durably committed (Pending) but before the payment provider has confirmed
/// the checkout session via webhook.
/// </summary>
public class BookingPendingPaymentTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private static Booking CreateBooking(PaymentMethod payment = PaymentMethod.Instant)
    {
        var result = Booking.Create(
            UserId.FromDatabase(Guid.NewGuid()),
            EventId.FromDatabase(Guid.NewGuid()),
            TicketTypeId.FromDatabase(Guid.NewGuid()),
            "Pending-First Test Event",
            quantity: 1,
            Money.FromDecimal(100m, "EGP").Value,
            payment,
            UtcNow);

        return result.Value;
    }

    #region MarkAsPendingPayment

    [Fact]
    public void MarkAsPendingPayment_WhenPending_ShouldTransitionToPendingPayment()
    {
        var booking = CreateBooking();

        var result = booking.MarkAsPendingPayment(UtcNow);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.PendingPayment);
    }

    [Theory]
    [InlineData(BookingStatusEnum.Confirmed)]
    [InlineData(BookingStatusEnum.Cancelled)]
    [InlineData(BookingStatusEnum.Expired)]
    [InlineData(BookingStatusEnum.Refunded)]
    public void MarkAsPendingPayment_WhenNotPending_ShouldReturnFailure(BookingStatusEnum initial)
    {
        var booking = CreateBooking();
        TransitionTo(booking, initial);

        var result = booking.MarkAsPendingPayment(UtcNow);

        result.IsFailure.Should().BeTrue(
            "only a Pending booking can move to PendingPayment");
        booking.Status.Should().Be(initial, "a failed transition must not mutate state");
    }

    [Fact]
    public void MarkAsPendingPayment_ShouldRaisePaymentInitiatedEvent()
    {
        var booking = CreateBooking();

        booking.MarkAsPendingPayment(UtcNow);

        booking.DomainEvents
            .Should().ContainSingle(e => e.GetType().Name == "PaymentInitiatedEvent");
    }

    #endregion

    #region MarkAsPending (revert)

    [Fact]
    public void MarkAsPending_WhenPendingPayment_ShouldRevertToPending()
    {
        var booking = CreateBooking();
        booking.MarkAsPendingPayment(UtcNow);

        var result = booking.MarkAsPending(UtcNow);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Pending);
    }

    [Theory]
    [InlineData(BookingStatusEnum.Pending)]
    [InlineData(BookingStatusEnum.Confirmed)]
    [InlineData(BookingStatusEnum.Cancelled)]
    [InlineData(BookingStatusEnum.Expired)]
    public void MarkAsPending_WhenNotPendingPayment_ShouldReturnFailure(BookingStatusEnum initial)
    {
        var booking = CreateBooking();
        TransitionTo(booking, initial);

        var result = booking.MarkAsPending(UtcNow);

        result.IsFailure.Should().BeTrue(
            "only a PendingPayment booking can revert to Pending");
        booking.Status.Should().Be(initial);
    }

    #endregion

    #region Confirm / Cancel / Expire from PendingPayment

    [Fact]
    public void Confirm_WhenPendingPayment_ShouldTransitionToConfirmed()
    {
        var booking = CreateBooking();
        booking.MarkAsPendingPayment(UtcNow);

        var result = booking.Confirm(UtcNow);

        result.IsSuccess.Should().BeTrue(
            "the webhook confirms the booking from either Pending or PendingPayment");
        booking.Status.Should().Be(BookingStatusEnum.Confirmed);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ShouldTransitionToCancelled()
    {
        var booking = CreateBooking();
        booking.MarkAsPendingPayment(UtcNow);

        var result = booking.Cancel(UtcNow, "Payment failed");

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Cancelled);
        booking.CancellationReason.Should().Be("Payment failed");
    }

    [Fact]
    public void Expire_WhenPendingPaymentAndHoldExpired_ShouldTransitionToExpired()
    {
        // Deferred bookings get a 30-minute hold.
        var booking = CreateBooking(PaymentMethod.Deferred);
        booking.MarkAsPendingPayment(UtcNow);
        var afterHold = UtcNow.AddMinutes(31);

        var result = booking.Expire(afterHold);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Expired);
    }

    #endregion

    #region CanBeModified / IsActive

    [Fact]
    public void CanBeModified_WhenPendingPayment_ShouldReturnTrue()
    {
        var booking = CreateBooking();
        booking.MarkAsPendingPayment(UtcNow);

        booking.CanBeModified().Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenPendingPayment_ShouldReturnTrue()
    {
        var booking = CreateBooking();
        booking.MarkAsPendingPayment(UtcNow);

        booking.IsActive().Should().BeTrue();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Drives a booking into the requested status through its legal transitions.
    /// Used by the Theory tests above to seed invalid starting states.
    /// </summary>
    private static void TransitionTo(Booking booking, BookingStatusEnum target)
    {
        switch (target)
        {
            case BookingStatusEnum.Confirmed:
                booking.Confirm(UtcNow);
                break;
            case BookingStatusEnum.Cancelled:
                booking.Cancel(UtcNow);
                break;
            case BookingStatusEnum.Expired:
                // Force expiry by pushing the clock past the hold window.
                booking.Expire(UtcNow.AddHours(1));
                break;
            case BookingStatusEnum.Refunded:
                booking.Cancel(UtcNow);
                booking.Refund(UtcNow);
                break;
        }
    }

    #endregion
}
