using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public abstract class TicketTypeUpdatedEvent : DomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }

        protected TicketTypeUpdatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(dateTimeProvider, metadata)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
        }
    }
}