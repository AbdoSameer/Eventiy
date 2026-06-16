using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Aggregates.EventAggregate.Errors;

internal static class EventErrors
{
    // ===== Creation & Existence =====
    public static string EventNotFound(EventId eventId)
        => $"Event with ID {eventId.Value} was not found.";

    public static string EventAlreadyExists(string eventName, DateTime eventDate)
        => $"An event named '{eventName}' already exists on {eventDate:d}.";

    public static string InvalidEventId(Guid eventId)
        => $"The provided event ID '{eventId}' is invalid.";

    // ===== Lifecycle Rules =====
    public static string InvalidEventDate(DateTime attemptedDate)
        => $"Event date {attemptedDate:yyyy-MM-dd} cannot be in the past.";

    public static string CannotPublishCancelledEvent()
        => "Cannot publish an event that is already cancelled.";

    public static string CannotCancelPublishedEvent()
        => "Cannot cancel an event that is already published.";

    public static string AlreadyCancelled()
        => "Event is already cancelled.";

    public static string CannotPublishWithoutTicketTypes()
        => "An event must have at least one ticket type before publishing.";

    // ===== Capacity Rules =====
    public static string InvalidTotalSeats(int attemptedSeats)
        => $"Total seats ({attemptedSeats}) must be greater than zero.";

    public static string TotalSeatsCannotBeLessThanAllocatedTickets(int totalSeats, int allocatedTickets)
        => $"Total seats ({totalSeats}) cannot be less than allocated ticket capacity ({allocatedTickets}).";

    public static string TicketTypeCapacityExceedsRemainingCapacity(int requested, int remaining)
        => $"Requested ticket capacity ({requested}) exceeds remaining event capacity ({remaining}).";

    public static string CannotChangeCapacityAfterPublish()
        => "Cannot change event capacity after it has been published.";

    // ===== Ticket Type Limits =====
    public static string MaxTicketTypesExceeded(int maxAllowed)
        => $"Event cannot have more than {maxAllowed} ticket types.";

    // Note: maxAllowed is passed from configuration or constant - NOT hardcoded here

    // ===== Common =====
    public static string LocationCannotBeEmpty()
        => "Event location cannot be empty.";

    public static string DescriptionTooLong(int maxLength)
        => $"Event description cannot exceed {maxLength} characters.";

    internal static string CannotCancelCompletedEvent()
        =>"Cannot cancel an event that has already been completed.";

    // Lifecycle Rules - New errors
    public static string AlreadyPublished()
        => "Event is already published.";

    public static string AlreadyCompleted()
        => "Event is already completed.";

    public static string CannotPublishCompletedEvent()
        => "Cannot publish a completed event.";

    public static string CannotCompleteCancelledEvent()
        => "Cannot complete a cancelled event.";

    public static string CannotCompleteDraftEvent()
        => "Cannot complete an event that hasn't been published yet.";

    public static string CannotCompleteFutureEvent(DateTime eventDate)
        => $"Cannot complete event with future date {eventDate:yyyy-MM-dd}.";

    public static string CanOnlyReopenCompletedEvent()
        => "Can only reopen a completed event.";

    public static string CannotReopenPastEvent(DateTime eventDate)
        => $"Cannot reopen an event that ended on {eventDate:yyyy-MM-dd}.";

    // Modification restrictions
    public static string CannotModifyTicketTypesAfterDraft()
        => "Cannot add or modify ticket types after event leaves Draft status.";

    public static string CannotModifyCapacityAfterDraft()
        => "Cannot modify event capacity after it leaves Draft status.";

    public static string CannotModifyDateAfterDraft()
        => "Cannot modify event date after it leaves Draft status.";

    public static string CannotModifyLocationAfterDraft()
        => "Cannot modify event location after it leaves Draft status.";

    public static string CannotModifyDescriptionAfterDraft()
        => "Cannot modify event description after it leaves Draft status.";

    public static string TotalSeatsCannotBeNegative(int capacityValue)
        => $"Total seats ({capacityValue}) cannot be negative.";
    
    // No GetUnexpectedErrorMessage - that belongs to Infrastructure layer
}