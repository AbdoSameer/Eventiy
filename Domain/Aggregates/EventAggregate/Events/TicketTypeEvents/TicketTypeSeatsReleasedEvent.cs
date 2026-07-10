using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeSeatsReleasedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public int QuantityReleased { get; }
        public int TotalSold { get; }
        public int AvailableRemaining { get; }

        public override string Name => nameof(TicketTypeSeatsReleasedEvent);
        public override string Domain => "Event";

        public TicketTypeSeatsReleasedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReleased,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            QuantityReleased = quantityReleased;
            TotalSold = totalSold;
            AvailableRemaining = availableRemaining;
        }
    }
}