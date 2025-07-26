using System.Text;
using System.Text.Json;
using System.Net.WebSockets;
using Microsoft.AspNetCore.SignalR;

namespace CollaborativePuzzle.Api.SocketIO
{
    /// <summary>
    /// Middleware that provides Socket.IO compatibility for SignalR
    /// Translates Socket.IO protocol to SignalR calls
    /// </summary>
    public class SocketIOMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHubContext<SocketIOHub> _hubContext;
        private readonly ILogger<SocketIOMiddleware> _logger;
        private readonly SocketIOProtocolHandler _protocolHandler;

        public SocketIOMiddleware(
            RequestDelegate next,
            IHubContext<SocketIOHub> hubContext,
            ILogger<SocketIOMiddleware> logger)
        {
            _next = next;
            _hubContext = hubContext;
            _logger = logger;
            _protocolHandler = new SocketIOProtocolHandler();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/socket.io/" && context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await HandleSocketIOConnection(webSocket, context);
            }
            else if (context.Request.Path == "/socket.io/" && context.Request.Method == "GET")
            {
                // Handle Socket.IO polling transport
                await HandleSocketIOPolling(context);
            }
            else
            {
                await _next(context);
            }
        }

        private async Task HandleSocketIOConnection(WebSocket webSocket, HttpContext context)
        {
            var connectionId = Guid.NewGuid().ToString();
            var buffer = new ArraySegment<byte>(new byte[4096]);

            try
            {
                _logger.LogInformation("Socket.IO WebSocket connection established: {ConnectionId}", connectionId);

                // Send Socket.IO handshake
                await SendSocketIOHandshake(webSocket, connectionId);

                // Add to SignalR group
                await _hubContext.Groups.AddToGroupAsync(connectionId, "socketio-clients");

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        await ProcessSocketIOMessage(webSocket, connectionId, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Socket.IO WebSocket handler");
            }
            finally
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, "socketio-clients");
                _logger.LogInformation("Socket.IO WebSocket connection closed: {ConnectionId}", connectionId);
            }
        }

        private async Task SendSocketIOHandshake(WebSocket webSocket, string connectionId)
        {
            var handshake = new
            {
                sid = connectionId,
                upgrades = new[] { "websocket" },
                pingInterval = 25000,
                pingTimeout = 60000
            };

            var message = $"0{JsonSerializer.Serialize(handshake)}";
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ProcessSocketIOMessage(WebSocket webSocket, string connectionId, string message)
        {
            try
            {
                var (type, eventName, data) = _protocolHandler.ParseMessage(message);

                switch (type)
                {
                    case SocketIOMessageType.Connect:
                        await SendSocketIOAck(webSocket, "40");
                        break;

                    case SocketIOMessageType.Event:
                        await HandleSocketIOEvent(webSocket, connectionId, eventName, data);
                        break;

                    case SocketIOMessageType.Ping:
                        await SendSocketIOAck(webSocket, "3");
                        break;

                    case SocketIOMessageType.Disconnect:
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Socket.IO message: {Message}", message);
            }
        }

        private async Task HandleSocketIOEvent(WebSocket webSocket, string connectionId, string? eventName, object? data)
        {
            if (string.IsNullOrEmpty(eventName))
                return;

            _logger.LogDebug("Socket.IO event received: {Event} from {ConnectionId}", eventName, connectionId);

            // Translate Socket.IO events to SignalR hub methods
            switch (eventName)
            {
                case "join":
                    if (data is JsonElement element && element.TryGetProperty("room", out var room))
                    {
                        var roomName = room.GetString() ?? "default";
                        await _hubContext.Groups.AddToGroupAsync(connectionId, roomName);
                        await SendSocketIOEvent(webSocket, "joined", new { room = roomName, id = connectionId });
                    }
                    break;

                case "message":
                    // Broadcast message to all Socket.IO clients
                    await _hubContext.Clients.Group("socketio-clients").SendAsync("ReceiveMessage", connectionId, data);
                    await SendSocketIOEvent(webSocket, "message-sent", new { success = true });
                    break;

                case "ping-test":
                    await SendSocketIOEvent(webSocket, "pong-test", new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                    break;

                default:
                    // Forward unknown events to SignalR hub
                    await _hubContext.Clients.All.SendAsync("SocketIOEvent", eventName, data);
                    break;
            }
        }

        private async Task SendSocketIOEvent(WebSocket webSocket, string eventName, object data)
        {
            var eventData = new[] { eventName, data };
            var message = $"42{JsonSerializer.Serialize(eventData)}";
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendSocketIOAck(WebSocket webSocket, string ackMessage)
        {
            var bytes = Encoding.UTF8.GetBytes(ackMessage);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task HandleSocketIOPolling(HttpContext context)
        {
            // Simplified polling transport support
            context.Response.ContentType = "text/plain";
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            
            var response = new
            {
                sid = Guid.NewGuid().ToString(),
                upgrades = new[] { "websocket" },
                pingInterval = 25000,
                pingTimeout = 60000
            };

            await context.Response.WriteAsync($"0{JsonSerializer.Serialize(response)}");
        }
    }

    public enum SocketIOMessageType
    {
        Connect = 0,
        Disconnect = 1,
        Event = 2,
        Ack = 3,
        Error = 4,
        BinaryEvent = 5,
        BinaryAck = 6,
        Ping = 7,
        Pong = 8
    }

    public class SocketIOProtocolHandler
    {
        public (SocketIOMessageType type, string? eventName, object? data) ParseMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length < 1)
                return (SocketIOMessageType.Error, null, null);

            var typeChar = message[0];
            var type = typeChar switch
            {
                '0' => SocketIOMessageType.Connect,
                '1' => SocketIOMessageType.Disconnect,
                '2' => SocketIOMessageType.Event,  // Socket.IO uses 2 for EVENT
                '3' => SocketIOMessageType.Ack,
                '4' => SocketIOMessageType.Error,
                _ => SocketIOMessageType.Error
            };
            
            // Handle ping/pong separately as they use different message format
            if (message == "2") type = SocketIOMessageType.Ping;
            if (message == "3") type = SocketIOMessageType.Pong;

            if (type == SocketIOMessageType.Event && message.Length > 2)
            {
                var jsonPart = message.Substring(2); // Skip "42"
                try
                {
                    var array = JsonSerializer.Deserialize<JsonElement[]>(jsonPart);
                    if (array != null && array.Length >= 1)
                    {
                        var eventName = array[0].GetString();
                        object? data = array.Length > 1 ? (object?)array[1] : null;
                        return (type, eventName, data);
                    }
                }
                catch (JsonException)
                {
                    // Invalid JSON
                }
            }

            return (type, null, null);
        }
    }

    public static class SocketIOMiddlewareExtensions
    {
        public static IApplicationBuilder UseSocketIO(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SocketIOMiddleware>();
        }
    }
}