using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Errors;

public static class EventErrors
{
    // ===== Validation Errors ===================================

    public static Error NameCannotBeEmpty()
        => Error.Validation(
            "Event.NameCannotBeEmpty",
            "Event name cannot be empty.");

    public static Error NameTooLong(int maxLength)
        => Error.Validation(
            "Event.NameTooLong",
            $"Event name cannot exceed {maxLength} characters.");

    public static Error DescriptionTooLong(int maxLength)
        => Error.Validation(
            "Event.DescriptionTooLong",
            $"Event description cannot exceed {maxLength} characters.");

    public static Error LocationCannotBeEmpty()
        => Error.Validation(
            "Event.LocationCannotBeEmpty",
            "Event location cannot be empty.");

    public static Error InvalidEventDate(DateTime attemptedDate)
        => Error.Validation(
            "Event.InvalidDate",
            $"Event date {attemptedDate:yyyy-MM-dd} cannot be in the past.");

    public static Error EventDateMustBeInFuture()
        => Error.Validation(
            "Event.EventDateMustBeInFuture",
            "Event date must be in the future.");

    public static Error InvalidTotalSeats(int attemptedSeats)
        => Error.Validation(
            "Event.InvalidTotalSeats",
            $"Total seats ({attemptedSeats}) must be greater than zero.");

    public static Error TotalSeatsCannotBeNegative(int capacityValue)
        => Error.Validation(
            "Event.TotalSeatsCannotBeNegative",
            $"Total seats ({capacityValue}) cannot be negative.");

    public static Error TicketNameCannotBeEmpty()
        => Error.Validation(
            "Event.TicketNameCannotBeEmpty",
            "Ticket type name cannot be empty.");

    public static Error TicketPriceMustBeGreaterThanZero()
        => Error.Validation(
            "Event.TicketPriceMustBeGreaterThanZero",
            "Ticket price must be greater than zero.");

    public static Error TicketCapacityMustBeGreaterThanZero()
        => Error.Validation(
            "Event.TicketCapacityMustBeGreaterThanZero",
            "Ticket capacity must be greater than zero.");

    // ===== Conflict/State Errors =====================
    public static Error AlreadyPublished()
        => Error.Conflict(
            "Event.AlreadyPublished",
            "Event is already published.");

    public static Error AlreadyCancelled()
        => Error.Conflict(
            "Event.AlreadyCancelled",
            "Event is already cancelled.");

    public static Error AlreadyCompleted()
        => Error.Conflict(
            "Event.AlreadyCompleted",
            "Event is already completed.");

    public static Error CannotPublishCancelledEvent()
        => Error.Conflict(
            "Event.CannotPublishCancelled",
            "Cannot publish an event that is already cancelled.");

    public static Error CannotPublishCompletedEvent()
        => Error.Conflict(
            "Event.CannotPublishCompleted",
            "Cannot publish a completed event.");

    public static Error CannotPublishWithoutTicketTypes()
        => Error.Conflict(
            "Event.CannotPublishWithoutTickets",
            "An event must have at least one ticket type before publishing.");

    public static Error CannotCancelCompletedEvent()
        => Error.Conflict(
            "Event.CannotCancelCompleted",
            "Cannot cancel an event that has already been completed.");

    public static Error CannotCancelPublishedEvent()
        => Error.Conflict(
            "Event.CannotCancelPublished",
            "Cannot cancel an event that is already published.");

    public static Error CannotCompleteCancelledEvent()
        => Error.Conflict(
            "Event.CannotCompleteCancelled",
            "Cannot complete a cancelled event.");

    public static Error CannotCompleteDraftEvent()
        => Error.Conflict(
            "Event.CannotCompleteDraft",
            "Cannot complete an event that hasn't been published yet.");

    public static Error CannotCompleteFutureEvent(DateTime eventDate)
        => Error.Conflict(
            "Event.CannotCompleteFuture",
            $"Cannot complete event with future date {eventDate:yyyy-MM-dd}.");

    public static Error CannotCompleteWithPendingReservations()
        => Error.Conflict(
            "Event.CannotCompleteWithPendingReservations",
            "Cannot complete event while there are pending seat reservations.");

    public static Error CanOnlyReopenCompletedEvent()
        => Error.Conflict(
            "Event.CanOnlyReopenCompleted",
            "Can only reopen a completed event.");

    public static Error CannotReopenPastEvent(DateTime eventDate)
        => Error.Conflict(
            "Event.CannotReopenPast",
            $"Cannot reopen an event that ended on {eventDate:yyyy-MM-dd}.");

    public static Error CannotModifyTicketTypesAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyTicketTypesAfterDraft",
            "Cannot add or modify ticket types after event leaves Draft status.");

    public static Error CannotModifyCapacityAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyCapacityAfterDraft",
            "Cannot modify event capacity after it leaves Draft status.");

    public static Error CannotModifyDateAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyDateAfterDraft",
            "Cannot modify event date after it leaves Draft status.");

    public static Error CannotModifyLocationAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyLocationAfterDraft",
            "Cannot modify event location after it leaves Draft status.");

    public static Error CannotModifyDescriptionAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyDescriptionAfterDraft",
            "Cannot modify event description after it leaves Draft status.");

    public static Error CannotModifyNameAfterDraft()
        => Error.Conflict(
            "Event.CannotModifyNameAfterDraft",
            "Cannot modify event name after it leaves Draft status.");

    // ===== Capacity Rules ====================
    public static Error TotalSeatsCannotBeLessThanAllocatedTickets(int totalSeats, int allocatedTickets)
        => Error.Conflict(
            "Event.TotalSeatsLessThanAllocated",
            $"Total seats ({totalSeats}) cannot be less than allocated ticket capacity ({allocatedTickets}).");

    public static Error TicketTypeCapacityExceedsRemainingCapacity(int requested, int remaining)
        => Error.Conflict(
            "Event.CapacityExceedsRemaining",
            $"Requested ticket capacity ({requested}) exceeds remaining event capacity ({remaining}).");

    public static Error MaxTicketTypesExceeded(int maxAllowed)
        => Error.Conflict(
            "Event.MaxTicketTypesExceeded",
            $"Event cannot have more than {maxAllowed} ticket types.");

    public static Error DuplicateTicketTypeName(string name)
        => Error.Conflict(
            "Event.DuplicateTicketTypeName",
            $"A ticket type with name '{name}' already exists.");

    public static Error MaxTicketTypesReached(int mAX_TICKET_TYPES)
        => Error.Conflict(
            "Event.MaxTicketTypesReached",
            $"Maximum number of ticket types ({mAX_TICKET_TYPES}) reached for this event.");

    // ===== Not Found Errors ===============================
    public static Error EventNotFound(EventId eventId)
        => Error.NotFound(
            "Event.NotFound",
            $"Event with ID {eventId.Value} was not found.");

    public static Error TicketTypeNotFound(Guid ticketTypeId)
        => Error.NotFound(
            "Event.TicketTypeNotFound",
            $"Ticket type with ID {ticketTypeId} was not found.");

    public static Error EventAlreadyExists(string eventName, DateTime eventDate)
        => Error.Conflict(
            "Event.AlreadyExists",
            $"An event named '{eventName}' already exists on {eventDate:d}.");

    public static Error InvalidEventId(Guid eventId)
        => Error.Validation(
            "Event.InvalidId",
            $"The provided event ID '{eventId}' is invalid.");

    public static Error EventModifiedConcurrently()
        => Error.Conflict(
            "Event.ModifiedConcurrently",
            "Event was modified by another user. Please refresh and try again.");

    public static Error NotFound(Guid id)
        => Error.NotFound(
            "Event.NotFound",
            $"Event with ID {id} was not found.");


    // ===== Photo Errors ===============================
    public static Error MaxPhotosReached(int maxAllowed)
        => Error.Validation(
            "Event.MaxPhotosReached",
            $"An event cannot have more than {maxAllowed} photos.");

    public static Error PhotoNotFound(EventPhotoId photoId)
        => Error.NotFound(
            "Event.PhotoNotFound",
            $"Photo with ID {photoId.Value} was not found.");

    // ===== Authorization Errors ==========
    //public static Error UserNotAuthorized(Guid userId, EventId eventId)
    //    => Error.Unauthorized(
    //        "Event.UserNotAuthorized",
    //        $"User {userId} is not authorized to modify event {eventId.Value}.");

    public static Error InvalidSectionCode(string code, EventType eventType)
        => Error.Validation(
            "Event.InvalidSectionCode",
            $"Section code '{code}' is not a valid section for {eventType} events.");

    // ===== Concurrency Errors ==============

    public static Error CanOnlyToggleHighDemandOnPublishedEvent()
        => Error.Conflict(
            "Event.CanOnlyToggleHighDemandOnPublished",
            "High-demand mode can only be toggled on a published event.");

    public static Error CannotReserveOnUnpublishedEvent()
        => Error.Conflict(
            "Event.CannotReserveOnUnpublished",
            "Seats cannot be reserved on an unpublished event. Publish the event first.");

    public static Error RedisInventoryUnavailable()
        => Error.Failure(
            "Event.RedisInventoryUnavailable",
            "Redis inventory store is unavailable and the event is in high-demand mode. Please retry shortly.");

    public static Error RedisInventoryShortfall(int requested, long remaining)
        => Error.Conflict(
            "Event.RedisInventoryShortfall",
            $"Requested quantity ({requested}) exceeds remaining inventory ({remaining}).");
}