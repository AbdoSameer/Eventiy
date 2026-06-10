using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;
public sealed class EventId: ValueObjectBase
{
    public Guid Id { get; }

    private EventId (Guid id)
    {
        Id = id;
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