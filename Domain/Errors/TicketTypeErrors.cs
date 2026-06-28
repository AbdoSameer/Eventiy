using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Errors
{
    public static class TicketTypeErrors
    {
        // Validation Errors (400 Bad Request)

        public static Error NameCannotBeEmpty()
            => Error.Validation(
                "TicketType.NameCannotBeEmpty",
                "Ticket type name cannot be empty.");

        public static Error NameTooLong(int maxLength)
            => Error.Validation(
                "TicketType.NameTooLong",
                $"Ticket type name cannot exceed {maxLength} characters.");

        public static Error PriceMustBeGreaterThanZero()
            => Error.Validation(
                "TicketType.PriceMustBeGreaterThanZero",
                "Ticket price must be greater than zero.");

        public static Error PriceCannotBeNull()
            => Error.Validation(
                "TicketType.PriceCannotBeNull",
                "Ticket price cannot be null.");

        public static Error PriceCannotBeNegative()
            => Error.Validation(
                "TicketType.PriceCannotBeNegative",
                "Ticket price cannot be negative.");

        public static Error CapacityMustBeGreaterThanZero()
            => Error.Validation(
                "TicketType.CapacityMustBeGreaterThanZero",
                "Ticket capacity must be greater than zero.");

        public static Error CapacityTooSmall(int minCapacity)
            => Error.Validation(
                "TicketType.CapacityTooSmall",
                $"Ticket capacity must be at least {minCapacity}.");

        public static Error InvalidCurrency()
            => Error.Validation(
                "TicketType.InvalidCurrency",
                "Invalid currency specified for ticket price. Must be a 3-letter ISO code.");

        public static Error EventIdRequired()
            => Error.Validation(
                "TicketType.EventIdRequired",
                "Event ID is required for creating a ticket type.");

        public static Error QuantityMustBeGreaterThanZero()
            => Error.Validation(
                "TicketType.QuantityMustBeGreaterThanZero",
                "Quantity must be greater than zero.");

        public static Error InvalidQuantity(int quantity)
            => Error.Validation(
                "TicketType.InvalidQuantity",
                $"Quantity '{quantity}' is invalid. Must be greater than zero.");

        // Conflict/State Errors (409 Conflict)

        public static Error CannotReduceCapacityBelowSoldTickets(int soldTickets)
            => Error.Conflict(
                "TicketType.CannotReduceCapacityBelowSold",
                $"Cannot reduce ticket capacity below the number of sold tickets ({soldTickets}).");

        public static Error CannotReduceCapacityBelowOccupied(int occupied, int newCapacity)
            => Error.Conflict(
                "TicketType.CannotReduceCapacityBelowOccupied",
                $"Cannot reduce ticket capacity to {newCapacity} because {occupied} seats are already occupied (sold + reserved).");

        public static Error CannotReduceCapacityBelowReserved(int reservedCount, int newCapacity)
            => Error.Conflict(
                "TicketType.CannotReduceCapacityBelowReserved",
                $"Cannot reduce ticket capacity to {newCapacity} because {reservedCount} seats are reserved.");

        public static Error CannotRemoveTicketTypeWithBookings(int bookingCount)
            => Error.Conflict(
                "TicketType.CannotRemoveWithBookings",
                $"Cannot remove ticket type with existing bookings ({bookingCount}).");

        public static Error CannotRemoveTicketTypeWithReservations(int reservationCount)
            => Error.Conflict(
                "TicketType.CannotRemoveWithReservations",
                $"Cannot remove ticket type with existing reservations ({reservationCount}).");

        public static Error CannotRemoveWithSoldTickets(int soldCount)
            => Error.Conflict(
                "TicketType.CannotRemoveWithSoldTickets",
                $"Cannot remove ticket type with sold tickets ({soldCount}).");

        public static Error CannotRemoveWithReservedTickets(int reservedCount)
            => Error.Conflict(
                "TicketType.CannotRemoveWithReservedTickets",
                $"Cannot remove ticket type with reserved tickets ({reservedCount}).");

        public static Error TicketTypeNotRemoved()
            => Error.Conflict(
                "TicketType.NotRemoved",
                "Ticket type is not removed.");

        public static Error CapacityExceedsEventRemainingCapacity(int remainingCapacity)
            => Error.Conflict(
                "TicketType.CapacityExceedsRemaining",
                $"Ticket capacity exceeds event's remaining capacity ({remainingCapacity}).");

        public static Error InsufficientAvailableSeats(int requested, int available)
            => Error.Conflict(
                "TicketType.InsufficientAvailableSeats",
                $"Insufficient available seats. Requested: {requested}, Available: {available}");

        public static Error CapacityExceeded(int capacity, int soldCount, int requested)
            => Error.Conflict(
                "TicketType.CapacityExceeded",
                $"Cannot sell {requested} tickets. Capacity: {capacity}, Already sold: {soldCount}.");

        public static Error CannotReleaseMoreThanSold(int requested, int sold)
            => Error.Conflict(
                "TicketType.CannotReleaseMoreThanSold",
                $"Cannot release more seats than sold. Requested: {requested}, Sold: {sold}");

        public static Error CannotReleaseMoreThanReserved(int requested, int reserved)
            => Error.Conflict(
                "TicketType.CannotReleaseMoreThanReserved",
                $"Cannot release more seats than reserved. Requested: {requested}, Reserved: {reserved}");

        public static Error CannotConfirmMoreThanReserved(int requested, int reserved)
            => Error.Conflict(
                "TicketType.CannotConfirmMoreThanReserved",
                $"Cannot confirm more seats than reserved. Requested: {requested}, Reserved: {reserved}");

        public static Error CannotRefundMoreThanSold(int requested, int sold)
            => Error.Conflict(
                "TicketType.CannotRefundMoreThanSold",
                $"Cannot refund more seats than sold. Requested: {requested}, Sold: {sold}");

        public static Error MaxTicketTypesPerEventExceeded(int maxAllowed)
            => Error.Conflict(
                "TicketType.MaxTicketTypesExceeded",
                $"Maximum number of ticket types per event ({maxAllowed}) exceeded.");

        public static Error DuplicateTicketTypeName(string name)
            => Error.Conflict(
                "TicketType.DuplicateName",
                $"A ticket type with name '{name}' already exists in this event.");

        public static Error CannotModifyTicketTypeAfterEventPublished()
            => Error.Conflict(
                "TicketType.CannotModifyAfterEventPublished",
                "Cannot modify ticket types after the event has been published.");

        public static Error NotEnoughReservedSeats(int available, int requested)
            => Error.Conflict(
                "TicketType.NotEnoughReservedSeats",
                $"Not enough reserved seats. Available: {available}, Requested: {requested}");

        public static Error CannotReleaseNonReservedSeats()
            => Error.Conflict(
                "TicketType.CannotReleaseNonReservedSeats",
                "Cannot release seats that are not reserved.");

        public static Error TicketTypeRemoved()
            => Error.Conflict(
                "TicketType.Removed",
                "This ticket type has been removed and cannot be modified.");

        // Not Found Errors (404 Not Found)

        public static Error TicketTypeNotFound(TicketTypeId ticketTypeId)
            => Error.NotFound(
                "TicketType.NotFound",
                $"Ticket type with ID {ticketTypeId.Value} was not found.");

        public static Error TicketTypeNotFoundInEvent(Guid ticketTypeId, EventId eventId)
            => Error.NotFound(
                "TicketType.NotFoundInEvent",
                $"Ticket type with ID {ticketTypeId} was not found in event {eventId.Value}.");

        public static Error TicketTypeNotFoundById(Guid ticketTypeId)
            => Error.NotFound(
                "TicketType.NotFoundById",
                $"Ticket type with ID {ticketTypeId} was not found.");

        // ============================================================
        // ✅ Authorization Errors (403 Forbidden / 401 Unauthorized)
        // ============================================================

        //public static Error UserNotAuthorizedToModify(Guid userId)
        //    => Error.Unauthorized(
        //        "TicketType.UnauthorizedModification",
        //        $"User {userId} is not authorized to modify this ticket type.");

        //public static Error UserNotAuthorizedToDelete(Guid userId)
        //    => Error.Unauthorized(
        //        "TicketType.UnauthorizedDeletion",
        //        $"User {userId} is not authorized to delete this ticket type.");
  
    }
}