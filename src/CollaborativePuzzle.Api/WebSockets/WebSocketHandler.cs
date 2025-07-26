using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace CollaborativePuzzle.Api.WebSockets
{
    /// <summary>
    /// Raw WebSocket handler for demonstrating WebSocket protocol without SignalR
    /// </summary>
    public class WebSocketHandler
    {
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly IServiceProvider _serviceProvider;

        public WebSocketHandler(ILogger<WebSocketHandler> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Handles a WebSocket connection
        /// </summary>
        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var connectionId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("WebSocket connection established: {ConnectionId}", connectionId);

            try
            {
                // Send welcome message
                await SendMessageAsync(webSocket, new
                {
                    type = "welcome",
                    connectionId = connectionId,
                    timestamp = DateTime.UtcNow,
                    protocols = new[] { "json", "binary" }
                });

                // Handle incoming messages
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed by client",
                            CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        await HandleTextMessageAsync(webSocket, message, connectionId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await HandleBinaryMessageAsync(webSocket, buffer.Array!, result.Count, connectionId);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error for connection {ConnectionId}", connectionId);
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.InternalServerError,
                        "Connection error",
                        CancellationToken.None);
                }
                
                _logger.LogInformation("WebSocket connection closed: {ConnectionId}", connectionId);
            }
        }

        private async Task HandleTextMessageAsync(WebSocket webSocket, string message, string connectionId)
        {
            _logger.LogDebug("Received text message from {ConnectionId}: {Message}", connectionId, message);

            try
            {
                var json = JsonDocument.Parse(message);
                var messageType = json.RootElement.GetProperty("type").GetString();

                switch (messageType)
                {
                    case "ping":
                        await SendMessageAsync(webSocket, new { type = "pong", timestamp = DateTime.UtcNow });
                        break;

                    case "echo":
                        var echoData = json.RootElement.GetProperty("data").GetString();
                        await SendMessageAsync(webSocket, new 
                        { 
                            type = "echo", 
                            data = echoData, 
                            timestamp = DateTime.UtcNow 
                        });
                        break;

                    case "broadcast":
                        // In a real implementation, this would broadcast to all connected clients
                        var broadcastData = json.RootElement.GetProperty("data");
                        await SendMessageAsync(webSocket, new 
                        { 
                            type = "broadcast", 
                            data = broadcastData, 
                            from = connectionId,
                            timestamp = DateTime.UtcNow 
                        });
                        break;

                    case "binary-request":
                        // Send a binary response
                        var binaryData = GenerateBinaryData();
                        await SendBinaryAsync(webSocket, binaryData);
                        break;

                    default:
                        await SendMessageAsync(webSocket, new 
                        { 
                            type = "error", 
                            message = $"Unknown message type: {messageType}" 
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling text message");
                await SendMessageAsync(webSocket, new 
                { 
                    type = "error", 
                    message = "Failed to process message" 
                });
            }
        }

        private async Task HandleBinaryMessageAsync(WebSocket webSocket, byte[] buffer, int count, string connectionId)
        {
            _logger.LogDebug("Received binary message from {ConnectionId}: {ByteCount} bytes", connectionId, count);

            // Echo back the binary data with a header
            var responseBuffer = new byte[count + 4];
            
            // Add a simple 4-byte header (message length)
            BitConverter.GetBytes(count).CopyTo(responseBuffer, 0);
            
            // Copy the original data
            Array.Copy(buffer, 0, responseBuffer, 4, count);

            await SendBinaryAsync(webSocket, responseBuffer);
        }

        private async Task SendMessageAsync(WebSocket webSocket, object message)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        private async Task SendBinaryAsync(WebSocket webSocket, byte[] data)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None);
        }

        private byte[] GenerateBinaryData()
        {
            // Generate some sample binary data
            var data = new byte[1024];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(data);
            
            // Add a header
            var header = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            var result = new byte[header.Length + data.Length];
            header.CopyTo(result, 0);
            data.CopyTo(result, header.Length);
            
            return result;
        }
    }
}