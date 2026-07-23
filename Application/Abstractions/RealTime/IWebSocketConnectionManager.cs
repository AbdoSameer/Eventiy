using System.Net.WebSockets;

namespace Application.Abstractions.RealTime;

public interface IWebSocketConnectionManager
{
    string Add(WebSocket socket, Guid eventId, string userId);
    void Remove(string connectionId);
    WebSocket? Get(string connectionId);
    IReadOnlyList<(string ConnectionId, WebSocket Socket)> GetConnections(Guid eventId);
    Task BroadcastToEventAsync(Guid eventId, string message, CancellationToken ct);
    Task SendToConnectionAsync(string connectionId, string message, CancellationToken ct);
}
