using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;
public sealed class EventId: ValueObjectBase
{
    public Guid Value { get; }
    protected EventId() { }

    public EventId (Guid value)
    {
        Value = value;
    }

    public static EventId CreateUuid(Guid id)
    {
        return new(id);
    }

    public static EventId CreateUnqiue()
    {
        return new(Guid.NewGuid());
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}