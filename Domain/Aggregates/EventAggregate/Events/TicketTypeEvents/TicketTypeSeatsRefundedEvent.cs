using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeSeatsRefundedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public int QuantityRefunded { get; }
        public int TotalSold { get; }
        public int AvailableRemaining { get; }

        public override string Name => nameof(TicketTypeSeatsRefundedEvent);
        public override string Domain => "Event";

        public TicketTypeSeatsRefundedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityRefunded,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            QuantityRefunded = quantityRefunded;
            TotalSold = totalSold;
            AvailableRemaining = availableRemaining;
        }
    }
}
