using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeSeatsReservedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public int QuantityReserved { get; }
        public int TotalSold { get; }
        public int AvailableRemaining { get; }

        public override string Name => nameof(TicketTypeSeatsReservedEvent);
        public override string Domain => "Event";

        public TicketTypeSeatsReservedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReserved,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            QuantityReserved = quantityReserved;
            TotalSold = totalSold;
            AvailableRemaining = availableRemaining;
        }
    }
}