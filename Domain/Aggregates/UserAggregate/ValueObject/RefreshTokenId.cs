using Domain.Common;

namespace Domain.Aggregates.UserAggregate.ValueObject;

public sealed class RefreshTokenId : ValueObjectBase
{
    public int Value { get; }

    protected RefreshTokenId() { }

    private RefreshTokenId(int value) => Value = value;

    public static RefreshTokenId FromDatabase(int value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
