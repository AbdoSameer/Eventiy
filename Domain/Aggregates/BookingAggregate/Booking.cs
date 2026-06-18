using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Aggregates.OrderAggregate
{
    public class Booking : AggregateRoot<Guid>
    {
        public UserId UserId { get; private set; }
        public EventId EventId { get; private set; }
        
        private Booking() : base(Guid.NewGuid()) { }

        public Booking(Guid id) : base(id) { }


        

    }
}
