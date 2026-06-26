using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.EventAggregate.ValueObject
{
    public class EventName : ValueObjectBase
    {
        public string Value { get; }

        private EventName(string value)
        {
            Value = value;
        }

        public static Result<EventName> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result<EventName>.Failure(EventErrors.NameCannotBeEmpty());

            if (value.Length > 100)
                return Result<EventName>.Failure(EventErrors.NameTooLong(100));

            return Result<EventName>.Success(new EventName(value.Trim()));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value;
    }
}