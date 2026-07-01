using Domain.Aggregates.BookingAggregate.Events;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Events;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Events.TicketTypeEvents;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.Events;

namespace Domain.Common
{
    public static class DomainEventFactory
    {
        // Booking events
        public static BookingCreatedEvent CreateBookingCreated(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            int quantity,
            decimal totalAmount,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingCreatedEvent(bookingId, userId, eventId, quantity, totalAmount, occurredOnUtc, metadata);
        }

        public static BookingConfirmedEvent CreateBookingConfirmed(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingConfirmedEvent(bookingId, userId, eventId, occurredOnUtc, metadata);
        }

        public static BookingCancelledEvent CreateBookingCancelled(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            string? reason,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingCancelledEvent(bookingId, userId, eventId, occurredOnUtc, metadata, reason);
        }

        public static BookingCancellationRequestedEvent CreateBookingCancellationRequested(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            string? reason,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingCancellationRequestedEvent(bookingId, userId, eventId, occurredOnUtc, metadata, reason);
        }

        public static BookingExpiredEvent CreateBookingExpired(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingExpiredEvent(bookingId, userId, eventId, occurredOnUtc, metadata);
        }

        public static BookingRefundedEvent CreateBookingRefunded(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            decimal refundAmount,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingRefundedEvent(bookingId, userId, eventId, refundAmount, occurredOnUtc, metadata);
        }

        public static BookingQuantityUpdatedEvent CreateBookingQuantityUpdated(
            BookingId bookingId,
            decimal oldTotalAmount,
            decimal newTotalAmount,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new BookingQuantityUpdatedEvent(bookingId, oldTotalAmount, newTotalAmount, occurredOnUtc, metadata);
        }

        // Event aggregate events
        public static EventCreatedEvent CreateEventCreated(
            EventId eventId,
            string name,
            DateTime date,
            int capacity,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new EventCreatedEvent(eventId, name, date, capacity, occurredOnUtc, metadata);
        }

        public static EventPublishedEvent CreateEventPublished(
            EventId eventId,
            int totalTicketTypes,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new EventPublishedEvent(eventId, totalTicketTypes, occurredOnUtc, metadata);
        }

        public static EventCancelledEvent CreateEventCancelled(
            EventId eventId,
            string? reason,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new EventCancelledEvent(eventId, occurredOnUtc, metadata, reason);
        }

        public static EventCompletedEvent CreateEventCompleted(
            EventId eventId,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new EventCompletedEvent(eventId, occurredOnUtc, metadata);
        }

        public static EventCapacityUpdatedEvent CreateEventCapacityUpdated(
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new EventCapacityUpdatedEvent(eventId, oldCapacity, newCapacity, occurredOnUtc, metadata);
        }

        // Ticket type events
        public static TicketTypeAddedEvent CreateTicketTypeAdded(
            EventId eventId,
            TicketTypeId ticketTypeId,
            string ticketName,
            decimal price,
            int capacity,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypeAddedEvent(eventId, ticketTypeId, ticketName, price, capacity, occurredOnUtc, metadata);
        }

        public static TicketTypePriceUpdatedEvent CreateTicketTypePriceUpdated(
            TicketTypeId ticketTypeId,
            EventId eventId,
            decimal oldPrice,
            decimal newPrice,
            string currency,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypePriceUpdatedEvent(ticketTypeId, eventId, oldPrice, newPrice, currency, occurredOnUtc, metadata);
        }

        public static TicketTypeCapacityUpdatedEvent CreateTicketTypeCapacityUpdated(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypeCapacityUpdatedEvent(ticketTypeId, eventId, oldCapacity, newCapacity, occurredOnUtc, metadata);
        }

        public static TicketTypeRemovedEvent CreateTicketTypeRemoved(
            TicketTypeId ticketTypeId,
            EventId eventId,
            string name,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypeRemovedEvent(ticketTypeId, eventId, name, occurredOnUtc, metadata);
        }

        public static TicketTypeSeatsReservedEvent CreateTicketTypeSeatsReserved(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReserved,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypeSeatsReservedEvent(ticketTypeId, eventId, quantityReserved, totalSold, availableRemaining, occurredOnUtc, metadata);
        }

        public static TicketTypeSeatsReleasedEvent CreateTicketTypeSeatsReleased(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReleased,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new TicketTypeSeatsReleasedEvent(ticketTypeId, eventId, quantityReleased, totalSold, availableRemaining, occurredOnUtc, metadata);
        }

        // User aggregate events
        public static UserRegisteredEvent CreateUserRegistered(
            UserId userId,
            Email email,
            DateTime occurredOnUtc,
            EventMetadata metadata)
        {
            return new UserRegisteredEvent(userId, email, occurredOnUtc, metadata);
        }
    }
}
