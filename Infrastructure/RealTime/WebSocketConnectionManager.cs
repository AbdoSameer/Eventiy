using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RealTime;

public interface IWebSocketConnectionManager
{
    string Add(WebSocket socket, Guid eventId, string userId);
    void Remove(string connectionId);
    WebSocket? Get(string connectionId);
    IReadOnlyList<(string ConnectionId, WebSocket Socket)> GetConnections(Guid eventId);
    Task BroadcastToEventAsync(Guid eventId, string message, CancellationToken ct);
    Task SendToConnectionAsync(string connectionId, string message, CancellationToken ct);
}

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, ConnectionEntry> _connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<string>> _eventGroups = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    {
        _logger = logger;
    }

    public string Add(WebSocket socket, Guid eventId, string userId)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var entry = new ConnectionEntry(connectionId, socket, eventId, userId);

        if (!_connections.TryAdd(connectionId, entry))
            return Add(socket, eventId, userId); // retry on collision

        _eventGroups.AddOrUpdate(
            eventId,
            _ => new ConcurrentBag<string> { connectionId },
            (_, bag) => { bag.Add(connectionId); return bag; });

        _logger.LogDebug("WS connection {Id} added for event {EventId}, user {User}",
            connectionId, eventId, userId);
        return connectionId;
    }

    public void Remove(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var entry))
            return;

        if (_eventGroups.TryGetValue(entry.EventId, out var bag))
        {
            bag = new ConcurrentBag<string>(bag.Where(id => id != connectionId));
            _eventGroups.TryUpdate(entry.EventId, bag, bag);
        }

        _logger.LogDebug("WS connection {Id} removed", connectionId);
    }

    public WebSocket? Get(string connectionId) =>
        _connections.TryGetValue(connectionId, out var entry) ? entry.Socket : null;

    public IReadOnlyList<(string ConnectionId, WebSocket Socket)> GetConnections(Guid eventId)
    {
        if (!_eventGroups.TryGetValue(eventId, out var bag))
            return Array.Empty<(string, WebSocket)>();

        return bag
            .Where(id => _connections.TryGetValue(id, out var e) && e.Socket.State == WebSocketState.Open)
            .Select(id => (id, _connections[id].Socket))
            .ToList();
    }

    public async Task BroadcastToEventAsync(Guid eventId, string message, CancellationToken ct)
    {
        var connections = GetConnections(eventId);
        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var (id, socket) in connections)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send WS message to {Id}", id);
                Remove(id);
            }
        }
    }

    public async Task SendToConnectionAsync(string connectionId, string message, CancellationToken ct)
    {
        var socket = Get(connectionId);
        if (socket is null || socket.State != WebSocketState.Open)
            return;

        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        try
        {
            await socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WS message to {Id}", connectionId);
            Remove(connectionId);
        }
    }

    private sealed record ConnectionEntry(
        string ConnectionId,
        WebSocket Socket,
        Guid EventId,
        string UserId);
}
