using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events.TicketTypeEvents
{
    /// <summary>
    /// Raised by the AtomicRedisReservationStrategy after an atomic Redis DECR
    /// succeeds. Consumed by a background processor to sync the final
    /// ReservedCount back to the SQL database (write-behind).
    /// </summary>
    public class TicketTypeRedisReservationSyncedEvent : DomainEvent, IDomainEvent
    {
        public TicketTypeId TicketTypeId { get; }
        public EventId EventId { get; }
        public int QuantityReserved { get; }
        public long RedisRemainingCount { get; }

        public override string Name => nameof(TicketTypeRedisReservationSyncedEvent);
        public override string Domain => "Event";

        public TicketTypeRedisReservationSyncedEvent(
            TicketTypeId ticketTypeId,
            EventId eventId,
            int quantityReserved,
            long redisRemainingCount,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            QuantityReserved = quantityReserved;
            RedisRemainingCount = redisRemainingCount;
        }
    }
}
