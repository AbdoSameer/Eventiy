namespace Application.Abstractions.RealTime;

public interface IRedisPubSubBroadcaster
{
    Task SubscribeToEventAsync(Guid eventId, Func<string, Task> onMessage, CancellationToken ct);
    Task UnsubscribeFromEventAsync(Guid eventId);
    Task PublishAsync(Guid eventId, string message);
}
