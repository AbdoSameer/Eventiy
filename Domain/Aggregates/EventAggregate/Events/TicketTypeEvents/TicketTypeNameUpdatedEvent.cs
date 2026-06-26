using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeNameUpdatedEvent : TicketTypeUpdatedEvent, IDomainEvent
    {
        public string OldName { get; }
        public string NewName { get; }
        public DateTime UpdatedAt { get; }
        public override string Name => nameof(TicketTypeNameUpdatedEvent);
        public override string Domain => "Event";

        public TicketTypeNameUpdatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            string oldName,
            string newName,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata) : base(ticketTypeId, eventId, dateTimeProvider, metadata)
        {
            OldName = oldName;
            NewName = newName;
            UpdatedAt = dateTimeProvider.UtcNow;
        }
    }
}