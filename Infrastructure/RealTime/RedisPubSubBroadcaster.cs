using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.RealTime;

public interface IRedisPubSubBroadcaster
{
    Task SubscribeToEventAsync(Guid eventId, Func<string, Task> onMessage, CancellationToken ct);
    Task UnsubscribeFromEventAsync(Guid eventId);
    Task PublishAsync(Guid eventId, string message);
}

public sealed class RedisPubSubBroadcaster : IRedisPubSubBroadcaster
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisPubSubBroadcaster> _logger;
    private readonly Dictionary<string, ChannelMessageQueue> _subscriptions = new();

    private static RedisChannel ChannelKey(Guid eventId) =>
        new RedisChannel($"seats:event:{eventId}", RedisChannel.PatternMode.Literal);

    public RedisPubSubBroadcaster(ConnectionMultiplexer multiplexer, ILogger<RedisPubSubBroadcaster> logger)
    {
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger;
    }

    public Task SubscribeToEventAsync(Guid eventId, Func<string, Task> onMessage, CancellationToken ct)
    {
        var channel = ChannelKey(eventId);

        if (_subscriptions.ContainsKey(channel))
        {
            _logger.LogDebug("Already subscribed to {Channel}", channel);
            return Task.CompletedTask;
        }

        var queue = _subscriber.Subscribe(channel);
        _subscriptions[channel] = queue;

        queue.OnMessage(async msg =>
        {
            try
            {
                var text = (string)msg.Message!;
                if (!string.IsNullOrEmpty(text))
                    await onMessage(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing pub/sub message on {Channel}", (string)channel);
            }
        });

        _logger.LogInformation("Subscribed to Redis channel {Channel}", (string)channel);
        return Task.CompletedTask;
    }

    public async Task UnsubscribeFromEventAsync(Guid eventId)
    {
        var channel = ChannelKey(eventId);

        if (_subscriptions.TryGetValue(channel, out var queue))
        {
            await queue.UnsubscribeAsync();
            _subscriptions.Remove(channel);
            _logger.LogInformation("Unsubscribed from Redis channel {Channel}", (string)channel);
        }
    }

    public async Task PublishAsync(Guid eventId, string message)
    {
        var channel = ChannelKey(eventId);
        await _subscriber.PublishAsync(channel, message);
    }
}
