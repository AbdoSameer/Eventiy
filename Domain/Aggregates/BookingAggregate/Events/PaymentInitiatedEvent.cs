using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.BookingAggregate.Events
{
    public class PaymentInitiatedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(PaymentInitiatedEvent);
        public override string Domain => "Booking";

        public BookingId BookingId { get; }
        public UserId UserId { get; }
        public EventId EventId { get; }
        public TicketTypeId TicketTypeId { get; }
        public DateTime InitiatedAt { get; }

        public PaymentInitiatedEvent(
            BookingId bookingId,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            BookingId = bookingId;
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            InitiatedAt = occurredOnUtc;
        }
    }
}
