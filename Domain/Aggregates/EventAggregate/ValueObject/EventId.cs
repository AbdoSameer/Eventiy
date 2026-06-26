using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.EventAggregate.ValueObject;
public sealed class EventId: ValueObjectBase
{
    public Guid Value { get; }
    protected EventId() { }

    private EventId(Guid value)
    {
        Value = value;
    }
    public static EventId FromDatabase(Guid value) => new EventId(value);
    
    public static Result<EventId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<EventId>.Failure(EventErrors.InvalidEventId(value));

        return Result<EventId>.Success(new EventId(value));
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}