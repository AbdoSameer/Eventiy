using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypeReservationConfirmedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public int QuantityConfirmed { get; }
        public int TotalSold { get; }
        public int AvailableRemaining { get; }

        public override string Name => nameof(TicketTypeReservationConfirmedEvent);
        public override string Domain => "Event";

        public TicketTypeReservationConfirmedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityConfirmed,
            int totalSold,
            int availableRemaining,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            QuantityConfirmed = quantityConfirmed;
            TotalSold = totalSold;
            AvailableRemaining = availableRemaining;
        }
    }
}
