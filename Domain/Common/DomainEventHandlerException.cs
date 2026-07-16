namespace Domain.Common;

public sealed class DomainEventHandlerException : Exception
{
    public string EventName { get; }
    public string HandlerName { get; }

    public DomainEventHandlerException(
        string eventName,
        string handlerName,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        EventName = eventName;
        HandlerName = handlerName;
    }
}
