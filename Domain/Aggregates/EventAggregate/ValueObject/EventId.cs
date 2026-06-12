using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;
public sealed class EventId: ValueObjectBase
{
    public Guid Value { get; }

    private EventId (Guid value)
    {
        Value = value;
    }

    public static EventId CreateUnqiue()
    {
        return new(Guid.NewGuid());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        throw new NotImplementedException();
    }

}