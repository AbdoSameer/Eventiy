using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject
{
    public sealed class EventCapacity : ValueObjectBase
    {
        public int Capacity { get; private set; }

        private EventCapacity(int capacity)
        {
            Capacity = capacity;
        }

        public static Result<EventCapacity> Create(int capacity)
        {
            if (capacity < 0)
                return Result<EventCapacity>.Failure("Capacity cannot be negative.");

            return Result<EventCapacity>.Success(new EventCapacity(capacity));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Capacity;
        }
    }
}