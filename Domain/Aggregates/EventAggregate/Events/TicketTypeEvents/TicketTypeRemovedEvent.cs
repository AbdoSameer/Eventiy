using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeRemovedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public string TicketTypeName { get; }
        public DateTime RemovedAt { get; }
        public override string Name => nameof(TicketTypeRemovedEvent);
        public override string Domain => "Event";

        public TicketTypeRemovedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            string name,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(dateTimeProvider, metadata)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            TicketTypeName = name;
            RemovedAt = dateTimeProvider.UtcNow;
        }
    }
}