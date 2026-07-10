using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    public class TicketTypePriceUpdatedEvent : TicketTypeUpdatedEvent, IDomainEvent
    {
        public decimal OldPrice { get; }
        public decimal NewPrice { get; }
        public string Currency { get; }
        public DateTime UpdatedAt { get; }
        public override string Name => nameof(TicketTypePriceUpdatedEvent);
        public override string Domain => "Event";

        public TicketTypePriceUpdatedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            decimal oldPrice,
            decimal newPrice,
            string currency,
            DateTime occurredOnUtc) : base(ticketTypeId, eventId, occurredOnUtc)
        {
            OldPrice = oldPrice;
            NewPrice = newPrice;
            Currency = currency;
            UpdatedAt = occurredOnUtc;
        }
    }
}