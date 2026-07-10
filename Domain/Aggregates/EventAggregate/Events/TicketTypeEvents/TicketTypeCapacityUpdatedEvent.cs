using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeCapacityUpdatedEvent : TicketTypeUpdatedEvent, IDomainEvent
    {
        public int OldCapacity { get; }
        public int NewCapacity { get; }
        public DateTime UpdatedAt { get; }
        public override string Name => nameof(TicketTypeCapacityUpdatedEvent);
        public override string Domain => "Event";

        public TicketTypeCapacityUpdatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int oldCapacity,
            int newCapacity,
            DateTime occurredOnUtc) : base(ticketTypeId, eventId, occurredOnUtc)
        {
            OldCapacity = oldCapacity;
            NewCapacity = newCapacity;
            UpdatedAt = occurredOnUtc;
        }
    }
}