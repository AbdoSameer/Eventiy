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
            DateTime occurredOnUtc,
            EventMetadata metadata) : base(occurredOnUtc, metadata)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
        }
    }
}