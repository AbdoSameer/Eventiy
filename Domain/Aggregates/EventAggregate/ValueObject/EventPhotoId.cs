using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.EventAggregate.ValueObject;

public sealed class EventPhotoId : ValueObjectBase
{
    public Guid Value { get; }

    protected EventPhotoId() { }

    private EventPhotoId(Guid value) => Value = value;

    public static EventPhotoId FromDatabase(Guid value) => new(value);

    public static Result<EventPhotoId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<EventPhotoId>.Failure(EventErrors.InvalidEventId(value));

        return Result<EventPhotoId>.Success(new EventPhotoId(value));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
