using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CollaborativePuzzle.Api.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests
{
    public class WebSocketHandlerTests : TestBase
    {
        private readonly WebSocketHandler _handler;
        private readonly Mock<ILogger<WebSocketHandler>> _mockLogger;
        private readonly ServiceProvider _serviceProvider;

        public WebSocketHandlerTests()
        {
            _mockLogger = new Mock<ILogger<WebSocketHandler>>();
            
            var services = new ServiceCollection();
            services.AddSingleton(_mockLogger.Object);
            _serviceProvider = services.BuildServiceProvider();
            
            _handler = new WebSocketHandler(_mockLogger.Object, _serviceProvider);
        }

        [Fact]
        public async Task HandleWebSocketAsync_SendsWelcomeMessage()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            var receivedMessages = new List<byte[]>();
            
            mockWebSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                    (buffer, type, endOfMessage, token) =>
                    {
                        receivedMessages.Add(buffer.Array.Take(buffer.Count).ToArray());
                    })
                .Returns(Task.CompletedTask);

            // Simulate immediate close
            var closeResult = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(closeResult);
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            Assert.Single(receivedMessages);
            var welcomeMessage = Encoding.UTF8.GetString(receivedMessages[0]);
            var welcomeData = JsonSerializer.Deserialize<JsonElement>(welcomeMessage);
            
            Assert.Equal("welcome", welcomeData.GetProperty("type").GetString());
            Assert.True(welcomeData.TryGetProperty("connectionId", out _));
            Assert.True(welcomeData.TryGetProperty("timestamp", out _));
            Assert.True(welcomeData.TryGetProperty("protocols", out _));
        }

        [Fact]
        public async Task HandleWebSocketAsync_HandlesPingMessage()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            var receivedMessages = new List<string>();
            var stateSequence = new Queue<WebSocketState>(new[] { 
                WebSocketState.Open, 
                WebSocketState.Open, 
                WebSocketState.Open 
            });
            
            mockWebSocket.Setup(ws => ws.State)
                .Returns(() => stateSequence.Count > 0 ? stateSequence.Dequeue() : WebSocketState.Closed);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                    (buffer, type, endOfMessage, token) =>
                    {
                        if (type == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                            receivedMessages.Add(message);
                        }
                    })
                .Returns(Task.CompletedTask);

            // Setup receive sequence
            var pingMessage = JsonSerializer.Serialize(new { type = "ping" });
            var pingBytes = Encoding.UTF8.GetBytes(pingMessage);
            var receiveSequence = new Queue<WebSocketReceiveResult>();
            
            // First receive: ping message
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                pingBytes.Length, WebSocketMessageType.Text, true));
            
            // Second receive: close
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, true));
            
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArraySegment<byte> buffer, CancellationToken token) =>
                {
                    var result = receiveSequence.Dequeue();
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        Array.Copy(pingBytes, 0, buffer.Array, buffer.Offset, pingBytes.Length);
                    }
                    return result;
                });
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            Assert.Equal(2, receivedMessages.Count); // Welcome + Pong
            
            var pongMessage = receivedMessages.FirstOrDefault(m => m.Contains("\"pong\""));
            Assert.NotNull(pongMessage);
            
            var pongData = JsonSerializer.Deserialize<JsonElement>(pongMessage);
            Assert.Equal("pong", pongData.GetProperty("type").GetString());
            Assert.True(pongData.TryGetProperty("timestamp", out _));
        }

        [Fact]
        public async Task HandleWebSocketAsync_HandlesEchoMessage()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            var receivedMessages = new List<string>();
            var stateSequence = new Queue<WebSocketState>(new[] { 
                WebSocketState.Open, 
                WebSocketState.Open, 
                WebSocketState.Open 
            });
            
            mockWebSocket.Setup(ws => ws.State)
                .Returns(() => stateSequence.Count > 0 ? stateSequence.Dequeue() : WebSocketState.Closed);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                    (buffer, type, endOfMessage, token) =>
                    {
                        if (type == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                            receivedMessages.Add(message);
                        }
                    })
                .Returns(Task.CompletedTask);

            // Setup receive sequence
            var echoData = "Hello, WebSocket!";
            var echoMessage = JsonSerializer.Serialize(new { type = "echo", data = echoData });
            var echoBytes = Encoding.UTF8.GetBytes(echoMessage);
            var receiveSequence = new Queue<WebSocketReceiveResult>();
            
            // First receive: echo message
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                echoBytes.Length, WebSocketMessageType.Text, true));
            
            // Second receive: close
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, true));
            
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArraySegment<byte> buffer, CancellationToken token) =>
                {
                    var result = receiveSequence.Dequeue();
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        Array.Copy(echoBytes, 0, buffer.Array, buffer.Offset, echoBytes.Length);
                    }
                    return result;
                });
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            var echoResponse = receivedMessages.FirstOrDefault(m => m.Contains("\"echo\"") && m.Contains(echoData));
            Assert.NotNull(echoResponse);
            
            var echoResponseData = JsonSerializer.Deserialize<JsonElement>(echoResponse);
            Assert.Equal("echo", echoResponseData.GetProperty("type").GetString());
            Assert.Equal(echoData, echoResponseData.GetProperty("data").GetString());
            Assert.True(echoResponseData.TryGetProperty("timestamp", out _));
        }

        [Fact]
        public async Task HandleWebSocketAsync_HandlesBinaryMessage()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            var receivedBinaryData = new List<byte[]>();
            var stateSequence = new Queue<WebSocketState>(new[] { 
                WebSocketState.Open, 
                WebSocketState.Open, 
                WebSocketState.Open 
            });
            
            mockWebSocket.Setup(ws => ws.State)
                .Returns(() => stateSequence.Count > 0 ? stateSequence.Dequeue() : WebSocketState.Closed);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                    (buffer, type, endOfMessage, token) =>
                    {
                        if (type == WebSocketMessageType.Binary)
                        {
                            receivedBinaryData.Add(buffer.Array.Skip(buffer.Offset).Take(buffer.Count).ToArray());
                        }
                    })
                .Returns(Task.CompletedTask);

            // Setup receive sequence
            var binaryData = new byte[] { 1, 2, 3, 4, 5 };
            var receiveSequence = new Queue<WebSocketReceiveResult>();
            
            // First receive: binary message
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                binaryData.Length, WebSocketMessageType.Binary, true));
            
            // Second receive: close
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, true));
            
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArraySegment<byte> buffer, CancellationToken token) =>
                {
                    var result = receiveSequence.Dequeue();
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        Array.Copy(binaryData, 0, buffer.Array, buffer.Offset, binaryData.Length);
                    }
                    return result;
                });
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            Assert.Single(receivedBinaryData);
            var response = receivedBinaryData[0];
            
            // Check header (first 4 bytes should contain the original message length)
            var headerLength = BitConverter.ToInt32(response, 0);
            Assert.Equal(binaryData.Length, headerLength);
            
            // Check that original data is included after header
            var dataPortionLength = response.Length - 4;
            Assert.Equal(binaryData.Length, dataPortionLength);
        }

        [Fact]
        public async Task HandleWebSocketAsync_HandlesUnknownMessageType()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            var receivedMessages = new List<string>();
            var stateSequence = new Queue<WebSocketState>(new[] { 
                WebSocketState.Open, 
                WebSocketState.Open, 
                WebSocketState.Open 
            });
            
            mockWebSocket.Setup(ws => ws.State)
                .Returns(() => stateSequence.Count > 0 ? stateSequence.Dequeue() : WebSocketState.Closed);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                    (buffer, type, endOfMessage, token) =>
                    {
                        if (type == WebSocketMessageType.Text)
                        {
                            var message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                            receivedMessages.Add(message);
                        }
                    })
                .Returns(Task.CompletedTask);

            // Setup receive sequence
            var unknownMessage = JsonSerializer.Serialize(new { type = "unknown-type", data = "test" });
            var unknownBytes = Encoding.UTF8.GetBytes(unknownMessage);
            var receiveSequence = new Queue<WebSocketReceiveResult>();
            
            // First receive: unknown message
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                unknownBytes.Length, WebSocketMessageType.Text, true));
            
            // Second receive: close
            receiveSequence.Enqueue(new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, true));
            
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ArraySegment<byte> buffer, CancellationToken token) =>
                {
                    var result = receiveSequence.Dequeue();
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        Array.Copy(unknownBytes, 0, buffer.Array, buffer.Offset, unknownBytes.Length);
                    }
                    return result;
                });
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            var errorMessage = receivedMessages.FirstOrDefault(m => m.Contains("\"error\""));
            Assert.NotNull(errorMessage);
            
            var errorData = JsonSerializer.Deserialize<JsonElement>(errorMessage);
            Assert.Equal("error", errorData.GetProperty("type").GetString());
            Assert.Contains("Unknown message type", errorData.GetProperty("message").GetString());
        }

        [Fact]
        public async Task HandleWebSocketAsync_LogsConnectionLifecycle()
        {
            // Arrange
            var mockWebSocket = new Mock<WebSocket>();
            mockWebSocket.Setup(ws => ws.State)
                .Returns(WebSocketState.Open);
            
            mockWebSocket.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Simulate immediate close
            var closeResult = new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            mockWebSocket.Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(closeResult);
            
            mockWebSocket.Setup(ws => ws.CloseAsync(
                    It.IsAny<WebSocketCloseStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleWebSocketAsync(mockWebSocket.Object);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("WebSocket connection established")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("WebSocket connection closed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        public override void Dispose()
        {
            _serviceProvider?.Dispose();
            base.Dispose();
        }
    }
}