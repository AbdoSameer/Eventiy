using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Infrastructure.RealTime;
using Microsoft.IdentityModel.Tokens;

namespace Eventy.WebApi.Middlewares;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<string, DateTime> _lastPong =
        new(StringComparer.Ordinal);

    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly TimeProvider _timeProvider;

    public WebSocketMiddleware(
        RequestDelegate next,
        ILogger<WebSocketMiddleware> logger,
        TokenValidationParameters tokenValidationParameters,
        TimeProvider timeProvider)
    {
        _next = next;
        _logger = logger;
        _tokenValidationParameters = tokenValidationParameters;
        _timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ws/venues", StringComparison.OrdinalIgnoreCase)
            || !context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        // ── Authenticate via JWT in query string ──
        var token = context.Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token) || !TryValidateToken(token, out var userId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        // ── Extract eventId from path ──
        var segments = context.Request.Path.Value!.Split('/');
        if (segments.Length < 4 || !Guid.TryParse(segments[3], out var eventId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var ct = context.RequestAborted;
        WebSocket? socket = null;
        IRedisPubSubBroadcaster? broadcaster = null;
        IWebSocketConnectionManager? connectionManager = null;
        var connectionId = string.Empty;

        try
        {
            socket = await context.WebSockets.AcceptWebSocketAsync();
            connectionManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
            broadcaster = context.RequestServices.GetRequiredService<IRedisPubSubBroadcaster>();

            connectionId = connectionManager.Add(socket, eventId, userId);
            _logger.LogInformation("WS connected: {Id} event={EventId} user={User}",
                connectionId, eventId, userId);

            // Send CONNECTED ack
            var connectedMsg = JsonSerializer.Serialize(new
            {
                type = "CONNECTED",
                connectionId,
            }, JsonOptions);
            await connectionManager.SendToConnectionAsync(connectionId, connectedMsg, ct);

            // Subscribe to Redis Pub/Sub for this event
            await broadcaster.SubscribeToEventAsync(eventId, async (message) =>
            {
                if (socket?.State == WebSocketState.Open)
                    await connectionManager.BroadcastToEventAsync(eventId, message, ct);
            }, ct);

            // ── Heartbeat + message loop ──
            _lastPong[connectionId] = _timeProvider.GetUtcNow().UtcDateTime;
            var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var heartbeatTask = RunHeartbeatAsync(socket, connectionId, connectionManager, heartbeatCts.Token);

            try
            {
                await ReceiveLoopAsync(socket, connectionId, eventId, userId, context, ct);
            }
            finally
            {
                await heartbeatCts.CancelAsync();
                try { await heartbeatTask; } catch { /* swallow */ }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for event {EventId}", eventId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("WebSocket cancelled for event {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected WebSocket error for event {EventId}", eventId);
        }
        finally
        {
            if (socket?.State == WebSocketState.Open || socket?.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);
                }
                catch { /* best-effort */ }
            }

            if (connectionManager is not null && !string.IsNullOrEmpty(connectionId))
            {
                connectionManager.Remove(connectionId);
            }

            if (broadcaster is not null)
            {
                try { await broadcaster.UnsubscribeFromEventAsync(eventId); }
                catch { /* best-effort */ }
            }

            _logger.LogInformation("WS disconnected for event {EventId}", eventId);
        }
    }

    private async Task ReceiveLoopAsync(
        WebSocket socket, string connectionId, Guid eventId, string userId,
        HttpContext context, CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                var connectionManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
                connectionManager.Remove(connectionId);
                break;
            }

            var raw = Encoding.UTF8.GetString(ms.ToArray());
            await ProcessMessageAsync(raw, connectionId, eventId, userId, context, ct);
        }
    }

    private async Task ProcessMessageAsync(
        string raw, string connectionId, Guid eventId, string userId,
        HttpContext context, CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch
        {
            return; // malformed – drop
        }

        var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        var connectionManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();

        switch (type)
        {
            case "PONG":
                _lastPong[connectionId] = _timeProvider.GetUtcNow().UtcDateTime;
                break;

            case "LOCK":
                await HandleLockAsync(doc, connectionId, eventId, userId, context, ct);
                break;

            case "UNLOCK":
                // Best-effort release – no need to push collision
                break;

            default:
                _logger.LogDebug("Unknown WS message type: {Type}", type);
                break;
        }

        doc.Dispose();
    }

    private async Task HandleLockAsync(
        JsonDocument doc, string connectionId, Guid eventId, string userId,
        HttpContext context, CancellationToken ct)
    {
        var root = doc.RootElement;
        var seatId = root.TryGetProperty("seatId", out var s) ? s.GetString() : null;
        var ticketTypeIdStr = root.TryGetProperty("ticketTypeId", out var tt) ? tt.GetString() : null;

        if (string.IsNullOrEmpty(seatId) || string.IsNullOrEmpty(ticketTypeIdStr)
            || !Guid.TryParse(ticketTypeIdStr, out var ticketTypeGuid))
        {
            return;
        }

        var connectionManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
        var broadcaster = context.RequestServices.GetRequiredService<IRedisPubSubBroadcaster>();
        var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebSocketMiddleware>>();

        try
        {
            var eventIdValue = Domain.Aggregates.EventAggregate.ValueObject.EventId.FromDatabase(eventId);
            var eventEntity = await eventRepo.GetByIdAsync(eventIdValue, ct);

            if (eventEntity is null)
            {
                await SendCollisionAsync(connectionManager, connectionId, seatId,
                    "Seat unavailable", ct);
                return;
            }

            var ticketTypeId = TicketTypeId.FromDatabase(ticketTypeGuid);
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            var reserveResult = eventEntity.ReserveSeats(ticketTypeId, 1, utcNow);
            if (reserveResult.IsFailure)
            {
                await SendCollisionAsync(connectionManager, connectionId, seatId,
                    reserveResult.Errors.FirstOrDefault()?.Message ?? "Cannot reserve", ct);
                return;
            }

            uow.EnforceFencingToken(eventEntity, eventEntity.RowVersion);

            await uow.CommitAsync(ct);

            // ── Broadcast DELTA to all subscribers ──
            var delta = SeatStateDelta.Delta(seatId, "RESERVED", _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            var deltaJson = JsonSerializer.Serialize(delta, JsonOptions);
            await broadcaster.PublishAsync(eventId, deltaJson);

            // Send ACK to the locker
            var ack = JsonSerializer.Serialize(new
            {
                type = "ACK",
                seatId,
                success = true,
            }, JsonOptions);
            await connectionManager.SendToConnectionAsync(connectionId, ack, ct);
        }
        catch (ConcurrencyException)
        {
            logger.LogWarning("Concurrency conflict on LOCK seat {Seat} event {Event}", seatId, eventId);
            await SendCollisionAsync(connectionManager, connectionId, seatId,
                "RESERVED_BY_OTHER", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LOCK failed for seat {Seat} event {Event}", seatId, eventId);
            await SendCollisionAsync(connectionManager, connectionId, seatId,
                "Internal error", ct);
        }
    }

    private async Task SendCollisionAsync(
        IWebSocketConnectionManager manager, string connectionId,
        string seatId, string reason, CancellationToken ct)
    {
        var collision = SeatStateDelta.Collision(seatId, reason,
            _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        var json = JsonSerializer.Serialize(collision, JsonOptions);
        await manager.SendToConnectionAsync(connectionId, json, ct);
    }

    private async Task RunHeartbeatAsync(
        WebSocket socket, string connectionId,
        IWebSocketConnectionManager manager, CancellationToken ct)
    {
        var ping = JsonSerializer.Serialize(new { type = "PING" }, JsonOptions);
        var pingBytes = Encoding.UTF8.GetBytes(ping);
        var pingSegment = new ArraySegment<byte>(pingBytes);

        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            await Task.Delay(HeartbeatInterval, ct);

            if (_lastPong.TryGetValue(connectionId, out var last)
                && _timeProvider.GetUtcNow().UtcDateTime - last > HeartbeatTimeout)
            {
                manager.Remove(connectionId);
                break;
            }

            try
            {
                await socket.SendAsync(pingSegment, WebSocketMessageType.Text, true, ct);
            }
            catch
            {
                manager.Remove(connectionId);
                break;
            }
        }
    }

    private bool TryValidateToken(string token, out string userId)
    {
        userId = string.Empty;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _tokenValidationParameters, out _);
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirst("sub");
            userId = sub?.Value ?? string.Empty;
            return !string.IsNullOrEmpty(userId);
        }
        catch
        {
            return false;
        }
    }
}
