using Domain.Aggregates.BookingAggregate.Events;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Events;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Events.TicketTypeEvents;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

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
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingCreatedEvent(bookingId, userId, eventId, quantity, totalAmount, dateTimeProvider, metadata);
        }

        public static BookingConfirmedEvent CreateBookingConfirmed(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingConfirmedEvent(bookingId, userId, eventId, dateTimeProvider, metadata);
        }

        public static BookingCancelledEvent CreateBookingCancelled(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            string? reason,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingCancelledEvent(bookingId, userId, eventId, dateTimeProvider, metadata, reason);
        }

        public static BookingCancellationRequestedEvent CreateBookingCancellationRequested(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            string? reason,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingCancellationRequestedEvent(bookingId, userId, eventId, dateTimeProvider, metadata, reason);
        }

        public static BookingExpiredEvent CreateBookingExpired(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingExpiredEvent(bookingId, userId, eventId, dateTimeProvider, metadata);
        }

        public static BookingRefundedEvent CreateBookingRefunded(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            decimal refundAmount,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingRefundedEvent(bookingId, userId, eventId, refundAmount, dateTimeProvider, metadata);
        }

        public static BookingQuantityUpdatedEvent CreateBookingQuantityUpdated(
            BookingId bookingId,
            decimal oldTotalAmount,
            decimal newTotalAmount,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new BookingQuantityUpdatedEvent(bookingId, oldTotalAmount, newTotalAmount, dateTimeProvider, metadata);
        }

        // Event aggregate events
        public static EventCreatedEvent CreateEventCreated(
            EventId eventId,
            string name,
            DateTime date,
            int capacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new EventCreatedEvent(eventId, name, date, capacity, dateTimeProvider, metadata);
        }

        public static EventPublishedEvent CreateEventPublished(
            EventId eventId,
            int totalTicketTypes,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new EventPublishedEvent(eventId, totalTicketTypes, dateTimeProvider, metadata);
        }

        public static EventCancelledEvent CreateEventCancelled(
            EventId eventId,
            string? reason,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new EventCancelledEvent(eventId, dateTimeProvider, metadata, reason);
        }

        public static EventCompletedEvent CreateEventCompleted(
            EventId eventId,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new EventCompletedEvent(eventId, dateTimeProvider, metadata);
        }

        public static EventCapacityUpdatedEvent CreateEventCapacityUpdated(
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new EventCapacityUpdatedEvent(eventId, oldCapacity, newCapacity, dateTimeProvider, metadata);
        }

        // Ticket type events
        public static TicketTypeAddedEvent CreateTicketTypeAdded(
            EventId eventId,
            TicketTypeId ticketTypeId,
            string ticketName,
            decimal price,
            int capacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypeAddedEvent(eventId, ticketTypeId, ticketName, price, capacity, dateTimeProvider, metadata);
        }

        public static TicketTypePriceUpdatedEvent CreateTicketTypePriceUpdated(
            TicketTypeId ticketTypeId,
            EventId eventId,
            decimal oldPrice,
            decimal newPrice,
            string currency,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypePriceUpdatedEvent(ticketTypeId, eventId, oldPrice, newPrice, currency, dateTimeProvider, metadata);
        }

        public static TicketTypeCapacityUpdatedEvent CreateTicketTypeCapacityUpdated(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypeCapacityUpdatedEvent(ticketTypeId, eventId, oldCapacity, newCapacity, dateTimeProvider, metadata);
        }

        public static TicketTypeRemovedEvent CreateTicketTypeRemoved(
            TicketTypeId ticketTypeId,
            EventId eventId,
            string name,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypeRemovedEvent(ticketTypeId, eventId, name, dateTimeProvider, metadata);
        }

        public static TicketTypeSeatsReservedEvent CreateTicketTypeSeatsReserved(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReserved,
            int totalSold,
            int availableRemaining,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypeSeatsReservedEvent(ticketTypeId, eventId, quantityReserved, totalSold, availableRemaining, dateTimeProvider, metadata);
        }

        public static TicketTypeSeatsReleasedEvent CreateTicketTypeSeatsReleased(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReleased,
            int totalSold,
            int availableRemaining,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            return new TicketTypeSeatsReleasedEvent(ticketTypeId, eventId, quantityReleased, totalSold, availableRemaining, dateTimeProvider, metadata);
        }
    }
}
