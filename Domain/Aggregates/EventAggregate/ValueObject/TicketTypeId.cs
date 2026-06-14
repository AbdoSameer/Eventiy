using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;

public sealed class TicketTypeId : ValueObjectBase
{
    public Guid Value { get; }

    public TicketTypeId(Guid value)
    {
        Value = value;
    }

    public static TicketTypeId CreateUnqiue()
    {
        return new(Guid.NewGuid());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}