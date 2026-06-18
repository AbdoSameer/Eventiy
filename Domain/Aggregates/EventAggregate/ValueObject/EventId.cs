using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;
public sealed class EventId: ValueObjectBase
{
    public Guid Value { get; }
    protected EventId() { }

    private EventId(Guid value)
    {
        Value = value;
    }

    public static Result<EventId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<EventId>.Failure("UserId cannot be empty");

        return Result<EventId>.Success(new EventId(value));
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}