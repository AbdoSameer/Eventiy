using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.EventAggregate.Events;

public sealed class EventPhotosUpdatedEvent : DomainEvent, IDomainEvent
{
    public EventId EventId { get; }
    public string Action { get; }
    public int PhotoCount { get; }
    public override string Name => nameof(EventPhotosUpdatedEvent);
    public override string Domain => "Event";

    public EventPhotosUpdatedEvent(
        EventId eventId,
        string action,
        int photoCount,
        DateTime occurredOnUtc) : base(occurredOnUtc)
    {
        EventId = eventId;
        Action = action;
        PhotoCount = photoCount;
    }
}
