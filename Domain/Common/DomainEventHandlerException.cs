namespace Domain.Common;

public sealed class DomainEventHandlerException : Exception
{
    public string EventType { get; }
    public string[] Errors { get; }

    public DomainEventHandlerException(string eventType, string[] errors)
        : base($"Handler for {eventType} failed: {string.Join(" | ", errors)}")
    {
        EventType = eventType;
        Errors = errors;
    }
}
