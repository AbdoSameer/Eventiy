using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject
{
    public sealed class EventName : ValueObjectBase
    {
        
        public string Name { get; private set; }

        private EventName(string name)
        {
            Name = name;
        }

        public static Result<EventName> Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<EventName>.Failure("Event name cannot be null or empty.");

            if (name.Length > 100)
                return Result<EventName>.Failure("Event name cannot be longer than 100 characters.");

            if (name.Length < 3)
                return Result<EventName>.Failure("Event name must be at least 3 characters long.");

            return Result<EventName>.Success(new EventName(name));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Name;
        }
    }
}