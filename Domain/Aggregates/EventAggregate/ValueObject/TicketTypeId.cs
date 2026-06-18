using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.ValueObject;

public sealed class TicketTypeId : ValueObjectBase
{
    public Guid Value { get; }

    private TicketTypeId(Guid value)
    {
        Value = value;
    }

    public static Result<TicketTypeId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<TicketTypeId>.Failure("UserId cannot be empty");

        return Result<TicketTypeId>.Success(new TicketTypeId(value));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

}