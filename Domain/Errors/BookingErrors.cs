using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Common;

namespace Domain.Errors
{
    public static class BookingErrors
    {
        // ===== Validation Errors =======
        public static Error QuantityMustBeGreaterThanZero()
            => Error.Validation(
                "Booking.QuantityMustBeGreaterThanZero",
                "Quantity must be greater than zero.");

        public static Error QuantityExceedsAvailableSeats(int requested, int available)
            => Error.Validation(
                "Booking.QuantityExceedsAvailableSeats",
                $"Requested quantity ({requested}) exceeds available seats ({available}).");

        public static Error MaximumQuantityExceeded(int maxQuantity)
            => Error.Validation(
                "Booking.MaximumQuantityExceeded",
                $"Cannot book more than {maxQuantity} tickets per booking.");

        public static Error BookingNotPending(Guid bookingId, BookingStatusEnum currentStatus)
            => Error.Validation(
                "Booking.BookingNotPending",
                $"Booking {bookingId} is not in pending status. Current status: {currentStatus}");

        public static Error InsufficientSeats(int requested, int available)
            => Error.Validation(
                "Booking.InsufficientSeats",
                $"Insufficient seats. Requested: {requested}, Available: {available}");

        public static Error InvalidMoneyAmount()
            => Error.Validation(
                "Booking.InvalidMoneyAmount",
                "Money amount must be greater than zero.");

        public static Error EventTitleCannotBeEmpty()
            => Error.Validation(
                "Booking.EventTitleCannotBeEmpty",
                "Event title cannot be empty.");

        public static Error BookingDateCannotBeInPast()
            => Error.Validation(
                "Booking.BookingDateCannotBeInPast",
                "Booking date cannot be in the past.");

        internal static Error CannotHoldBooking(Guid bookingId, BookingStatusEnum currentStatus)
            => Error.Validation(
                    "Booking.CannotHoldBooking",
                    $"Booking {bookingId} cannot be held from status {currentStatus}.");

        internal static Error HoldExpired(Guid bookingId)
            => Error.Validation(
                    "Booking.HoldExpired",
                   $"Booking {bookingId} hold period has expired.");

        internal static Error CannotExpireBooking(Guid bookingId, BookingStatusEnum currentStatus)
            => Error.Validation(
                    "Booking.CannotExpireBooking",
            $"Booking {bookingId} cannot expire from status {currentStatus}.");

        internal static Error HoldNotYetExpired(Guid bookingId)
            => Error.Validation(
                    "Booking.HoldNotYetExpired",
                   $"Booking {bookingId} hold period has not expired yet.");

        // ===== Conflict/State Errors =======
        public static Error BookingAlreadyCancelled(Guid bookingId)
            => Error.Conflict(
                "Booking.BookingAlreadyCancelled",
                $"Booking {bookingId} has already been cancelled.");

        public static Error BookingAlreadyConfirmed(Guid bookingId)
            => Error.Conflict(
                "Booking.BookingAlreadyConfirmed",
                $"Booking {bookingId} is already confirmed.");

        public static Error BookingExpired(Guid bookingId)
            => Error.Conflict(
                "Booking.BookingExpired",
                $"Booking {bookingId} has expired.");

        public static Error RefundNotAllowed(Guid bookingId)
            => Error.Conflict(
                "Booking.RefundNotAllowed",
                $"Refund not allowed for booking {bookingId}. Only cancelled bookings can be refunded.");

        public static Error CannotCancelBooking(Guid bookingId, BookingStatusEnum currentStatus)
            => Error.Conflict(
                "Booking.CannotCancelBooking",
                $"Cannot cancel booking {bookingId} with status: {currentStatus}. Only pending bookings can be cancelled.");

        public static Error CannotCancelConfirmedBooking(Guid bookingId)
            => Error.Conflict(
                "Booking.CannotCancelConfirmedBooking",
                $"Cannot cancel confirmed booking {bookingId}. Please request a cancellation instead.");

        public static Error CannotModifyExpiredBooking(Guid bookingId)
            => Error.Conflict(
                "Booking.CannotModifyExpiredBooking",
                $"Cannot modify expired booking {bookingId}.");

        public static Error CannotModifyRefundedBooking(Guid bookingId)
            => Error.Conflict(
                "Booking.CannotModifyRefundedBooking",
                $"Cannot modify refunded booking {bookingId}.");

        public static Error BookingAlreadyRefunded(Guid bookingId)
            => Error.Conflict(
                "Booking.BookingAlreadyRefunded",
                $"Booking {bookingId} has already been refunded.");

        public static Error RefundPeriodExpired(Guid bookingId, DateTime expiryDate)
            => Error.Conflict(
                "Booking.RefundPeriodExpired",
                $"Refund period has expired for booking {bookingId}. Expiry date: {expiryDate}");

        // ===== Not Found Errors =======
        public static Error BookingNotFound(Guid bookingId)
            => Error.NotFound(
                "Booking.BookingNotFound",
                $"Booking not found: {bookingId}");

        public static Error UserNotFound(Guid userId)
            => Error.NotFound(
                "Booking.UserNotFound",
                $"User not found: {userId}");

        public static Error EventNotFound(Guid eventId)
            => Error.NotFound(
                "Booking.EventNotFound",
                $"Event not found: {eventId}");

        public static Error TicketTypeNotFound(Guid ticketTypeId)
            => Error.NotFound(
                "Booking.TicketTypeNotFound",
                $"Ticket type not found: {ticketTypeId}");

        // ===== Authorization Errors ======
        //public static Error UserNotAuthorized(Guid userId, Guid bookingId)
        //    => Error.Unauthorized(
        //        "Booking.UserNotAuthorized",
        //        $"User {userId} is not authorized to access booking {bookingId}.");

       
        // ===== Concurrency Errors ======
        public static Error BookingModifiedConcurrently()
            => Error.Conflict(
                "Booking.ModifiedConcurrently",
                "Booking was modified by another user. Please refresh and try again.");

        public static Error InvalidBookingId(Guid BookingId)
            => Error.Validation(
                "Booking.InvalidId",
                $"The provided Booking ID '{BookingId}' is invalid.");

        public static Error BookingCreationFailed()
            => Error.Failure(
                "Booking.CreationFailed",
                "Failed to create the booking due to an unexpected error.");

        public static Error BookingConfirmationFailed()
            => Error.Failure(
                "Booking.ConfirmationFailed",
                "Failed to confirm the booking due to an unexpected error.");

        public static Error ConcurrencyConflict()
            => Error.Conflict(
                "Booking.CreationFailed",
                "Failed to create the booking due to a concurrency conflict. Please try again.");

        public static Error CannotConfirmBooking(Guid value)
            => Error.Conflict(
                "Booking.CannotConfirmBooking",
               $"Your acess to confirm booking {value} is not allow");
    }
}