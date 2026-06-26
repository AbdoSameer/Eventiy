using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Errors
{
    public static class TicketTypeErrors
    {
        // ===== Validation Errors ======
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

        public static Error CapacityMustBeGreaterThanZero()
            => Error.Validation(
                "TicketType.CapacityMustBeGreaterThanZero",
                "Ticket capacity must be greater than zero.");

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

        // ===== Conflict/State Errors ======
        public static Error CannotReduceCapacityBelowSoldTickets(int soldTickets)
            => Error.Conflict(
                "TicketType.CannotReduceCapacityBelowSold",
                $"Cannot reduce ticket capacity below the number of sold tickets ({soldTickets}).");

        public static Error CannotRemoveTicketTypeWithBookings(int bookingCount)
            => Error.Conflict(
                "TicketType.CannotRemoveWithBookings",
                $"Cannot remove ticket type with existing bookings ({bookingCount}).");

        public static Error CapacityExceedsEventRemainingCapacity(int remainingCapacity)
            => Error.Conflict(
                "TicketType.CapacityExceedsRemaining",
                $"Ticket capacity exceeds event's remaining capacity ({remainingCapacity}).");

        public static Error InsufficientAvailableSeats(int requested, int available)
            => Error.Conflict(
                "TicketType.InsufficientAvailableSeats",
                $"Insufficient available seats. Requested: {requested}, Available: {available}");

        public static Error CannotReleaseMoreThanSold(int requested, int sold)
            => Error.Conflict(
                "TicketType.CannotReleaseMoreThanSold",
                $"Cannot release more seats than sold. Requested: {requested}, Sold: {sold}");

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

        // ===== Not Found Errors ======
        public static Error TicketTypeNotFound(TicketTypeId ticketTypeId)
            => Error.NotFound(
                "TicketType.NotFound",
                $"Ticket type with ID {ticketTypeId.Value} was not found.");

        public static Error TicketTypeNotFoundInEvent(Guid ticketTypeId, EventId eventId)
            => Error.NotFound(
                "TicketType.NotFoundInEvent",
                $"Ticket type with ID {ticketTypeId} was not found in event {eventId.Value}.");

        // ===== Authorization Errors ======

        //    public static Error UserNotAuthorizedToModify(Guid userId)
        //        => Error.Unauthorized(
        //            "TicketType.UnauthorizedModification",
        //            $"User {userId} is not authorized to modify this ticket type.");
    }
}