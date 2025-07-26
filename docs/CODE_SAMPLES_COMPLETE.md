# Complete Code Samples & Implementation Examples

This document provides comprehensive code examples for the Collaborative Puzzle Platform, demonstrating all major features and implementation patterns.

## Table of Contents

1. [SignalR Real-Time Communication](#1-signalr-real-time-communication)
2. [WebSocket Raw Implementation](#2-websocket-raw-implementation)
3. [WebRTC Signaling](#3-webrtc-signaling)
4. [MQTT IoT Integration](#4-mqtt-iot-integration)
5. [Socket.IO Compatibility](#5-socketio-compatibility)
6. [Minimal API Endpoints](#6-minimal-api-endpoints)
7. [Repository Pattern](#7-repository-pattern)
8. [Redis Caching](#8-redis-caching)
9. [Health Checks](#9-health-checks)
10. [Security Implementation](#10-security-implementation)
11. [Kubernetes Configuration](#11-kubernetes-configuration)
12. [Docker Setup](#12-docker-setup)

## 1. SignalR Real-Time Communication

### SignalR Hub Implementation
```csharp
// Hubs/PuzzleHub.cs
public class PuzzleHub : Hub<IPuzzleClient>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IPieceRepository _pieceRepository;
    private readonly IRedisService _redisService;
    private readonly ILogger<PuzzleHub> _logger;

    public PuzzleHub(
        ISessionRepository sessionRepository,
        IPieceRepository pieceRepository,
        IRedisService redisService,
        ILogger<PuzzleHub> logger)
    {
        _sessionRepository = sessionRepository;
        _pieceRepository = pieceRepository;
        _redisService = redisService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task JoinPuzzleSession(string sessionId, string userId)
    {
        try
        {
            _logger.LogInformation("User {UserId} joining session {SessionId}", userId, sessionId);
            
            // Add to SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            
            // Update session in repository
            var participant = await _sessionRepository.AddParticipantAsync(
                Guid.Parse(sessionId), 
                Guid.Parse(userId), 
                Context.ConnectionId);
            
            // Store connection mapping in Redis
            await _redisService.SetAsync($"connection:{Context.ConnectionId}", new ConnectionInfo
            {
                UserId = userId,
                SessionId = sessionId,
                ConnectedAt = DateTime.UtcNow
            });
            
            // Notify others in the session
            await Clients.OthersInGroup(sessionId).UserJoined(new UserJoinedNotification
            {
                UserId = userId,
                Username = participant.Username,
                JoinedAt = DateTime.UtcNow
            });
            
            // Send current session state to the new user
            var sessionState = await GetSessionStateAsync(sessionId);
            await Clients.Caller.SessionStateUpdate(sessionState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
            await Clients.Caller.Error("Failed to join session", ex.Message);
        }
    }

    public async Task MovePiece(Guid sessionId, Guid pieceId, double x, double y, int rotation)
    {
        try
        {
            // Acquire distributed lock
            var lockKey = $"piece-lock:{pieceId}";
            var lockAcquired = await _redisService.TryAcquireLockAsync(lockKey, Context.ConnectionId, TimeSpan.FromSeconds(5));
            
            if (!lockAcquired)
            {
                await Clients.Caller.Error("Piece is locked by another user", "PIECE_LOCKED");
                return;
            }

            // Update piece position
            var result = await _pieceRepository.UpdatePiecePositionAsync(pieceId, x, y, rotation);
            
            if (result.Success)
            {
                // Broadcast to all users in session
                await Clients.Group(sessionId.ToString()).PieceMoved(new PieceMovedNotification
                {
                    PieceId = pieceId,
                    X = x,
                    Y = y,
                    Rotation = rotation,
                    MovedBy = Context.UserIdentifier,
                    IsCorrectlyPlaced = result.IsPlaced
                });
                
                // Check if puzzle is complete
                if (result.PuzzleCompleted)
                {
                    await Clients.Group(sessionId.ToString()).PuzzleCompleted(new PuzzleCompletedNotification
                    {
                        CompletedAt = DateTime.UtcNow,
                        TotalTime = result.CompletionTime,
                        Participants = result.Participants
                    });
                }
            }
        }
        finally
        {
            // Release lock
            await _redisService.ReleaseLockAsync($"piece-lock:{pieceId}", Context.ConnectionId);
        }
    }

    public async Task SendChatMessage(string sessionId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 500)
        {
            await Clients.Caller.Error("Invalid message", "INVALID_MESSAGE");
            return;
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.Parse(sessionId),
            UserId = Context.UserIdentifier,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        // Store in Redis for persistence
        await _redisService.AddToListAsync($"chat:{sessionId}", chatMessage, maxLength: 100);

        // Broadcast to session
        await Clients.Group(sessionId).ChatMessageReceived(chatMessage);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionInfo = await _redisService.GetAsync<ConnectionInfo>($"connection:{Context.ConnectionId}");
        
        if (connectionInfo != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, connectionInfo.SessionId);
            await _sessionRepository.RemoveParticipantAsync(Guid.Parse(connectionInfo.SessionId), Guid.Parse(connectionInfo.UserId));
            await _redisService.DeleteAsync($"connection:{Context.ConnectionId}");
            
            await Clients.Group(connectionInfo.SessionId).UserLeft(new UserLeftNotification
            {
                UserId = connectionInfo.UserId,
                LeftAt = DateTime.UtcNow
            });
        }

        await base.OnDisconnectedAsync(exception);
    }
}

// Client interface
public interface IPuzzleClient
{
    Task Connected(string connectionId);
    Task SessionStateUpdate(SessionState state);
    Task UserJoined(UserJoinedNotification notification);
    Task UserLeft(UserLeftNotification notification);
    Task PieceMoved(PieceMovedNotification notification);
    Task PieceLockedByUser(PieceLockedNotification notification);
    Task PuzzleCompleted(PuzzleCompletedNotification notification);
    Task ChatMessageReceived(ChatMessage message);
    Task Error(string message, string code);
}
```

### SignalR Client (TypeScript)
```typescript
// services/signalr-service.ts
import * as signalR from '@microsoft/signalr';

export class SignalRService {
    private connection: signalR.HubConnection;
    private sessionId: string;
    private userId: string;

    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/puzzlehub', {
                accessTokenFactory: () => this.getAccessToken()
            })
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupEventHandlers();
    }

    private setupEventHandlers(): void {
        this.connection.on('SessionStateUpdate', (state: SessionState) => {
            store.dispatch('puzzle/updateSessionState', state);
        });

        this.connection.on('PieceMoved', (notification: PieceMovedNotification) => {
            store.dispatch('puzzle/handlePieceMoved', notification);
        });

        this.connection.on('UserJoined', (notification: UserJoinedNotification) => {
            store.dispatch('session/addParticipant', notification);
            this.showNotification(`${notification.username} joined the puzzle`);
        });

        this.connection.on('PuzzleCompleted', (notification: PuzzleCompletedNotification) => {
            store.dispatch('puzzle/markCompleted', notification);
            this.showCelebration();
        });

        this.connection.onreconnecting(() => {
            console.log('Reconnecting to SignalR...');
            store.dispatch('connection/setStatus', 'reconnecting');
        });

        this.connection.onreconnected(() => {
            console.log('Reconnected to SignalR');
            store.dispatch('connection/setStatus', 'connected');
            // Rejoin session after reconnection
            if (this.sessionId && this.userId) {
                this.joinSession(this.sessionId, this.userId);
            }
        });
    }

    public async start(): Promise<void> {
        try {
            await this.connection.start();
            console.log('SignalR connected');
        } catch (err) {
            console.error('SignalR connection error:', err);
            setTimeout(() => this.start(), 5000);
        }
    }

    public async joinSession(sessionId: string, userId: string): Promise<void> {
        this.sessionId = sessionId;
        this.userId = userId;
        await this.connection.invoke('JoinPuzzleSession', sessionId, userId);
    }

    public async movePiece(pieceId: string, x: number, y: number, rotation: number): Promise<void> {
        await this.connection.invoke('MovePiece', this.sessionId, pieceId, x, y, rotation);
    }

    public async sendMessage(message: string): Promise<void> {
        await this.connection.invoke('SendChatMessage', this.sessionId, message);
    }
}
```

## 2. WebSocket Raw Implementation

### WebSocket Handler
```csharp
// WebSockets/WebSocketHandler.cs
public class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection
        {
            Id = connectionId,
            WebSocket = webSocket,
            ConnectedAt = DateTime.UtcNow
        };

        _connections.TryAdd(connectionId, connection);
        _logger.LogInformation("WebSocket connected: {ConnectionId}", connectionId);

        try
        {
            await HandleMessagesAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error for connection {ConnectionId}", connectionId);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Error occurred", CancellationToken.None);
            }
        }
    }

    private async Task HandleMessagesAsync(WebSocketConnection connection)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);

        while (connection.WebSocket.State == WebSocketState.Open)
        {
            var result = await connection.WebSocket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                await ProcessTextMessageAsync(connection, message);
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                await ProcessBinaryMessageAsync(connection, buffer.Array.Take(result.Count).ToArray());
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }
        }
    }

    private async Task ProcessTextMessageAsync(WebSocketConnection connection, string message)
    {
        try
        {
            var parsedMessage = JsonSerializer.Deserialize<WebSocketMessage>(message);
            
            switch (parsedMessage.Type)
            {
                case "ping":
                    await SendMessageAsync(connection, new { type = "pong", timestamp = DateTime.UtcNow });
                    break;
                    
                case "subscribe":
                    connection.Subscriptions.Add(parsedMessage.Channel);
                    await SendMessageAsync(connection, new { type = "subscribed", channel = parsedMessage.Channel });
                    break;
                    
                case "broadcast":
                    await BroadcastToChannelAsync(parsedMessage.Channel, parsedMessage.Data);
                    break;
                    
                default:
                    await SendMessageAsync(connection, new { type = "error", message = "Unknown message type" });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message");
            await SendMessageAsync(connection, new { type = "error", message = "Invalid message format" });
        }
    }

    private async Task BroadcastToChannelAsync(string channel, object data)
    {
        var connections = _connections.Values.Where(c => c.Subscriptions.Contains(channel));
        var message = new { type = "message", channel, data, timestamp = DateTime.UtcNow };
        
        var tasks = connections.Select(c => SendMessageAsync(c, message));
        await Task.WhenAll(tasks);
    }

    private async Task SendMessageAsync(WebSocketConnection connection, object message)
    {
        if (connection.WebSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await connection.WebSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}

// WebSocket connection model
public class WebSocketConnection
{
    public string Id { get; set; }
    public WebSocket WebSocket { get; set; }
    public DateTime ConnectedAt { get; set; }
    public HashSet<string> Subscriptions { get; set; } = new();
}
```

## 3. WebRTC Signaling

### WebRTC Hub
```csharp
// WebRTC/WebRTCHub.cs
public class WebRTCHub : Hub
{
    private static readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();
    private readonly ILogger<WebRTCHub> _logger;

    public async Task JoinRoom(string roomName, string userName)
    {
        _logger.LogInformation("User {UserName} joining room {RoomName}", userName, roomName);

        var roomInfo = _rooms.GetOrAdd(roomName, new RoomInfo { RoomName = roomName });
        
        lock (roomInfo.Participants)
        {
            roomInfo.Participants[Context.ConnectionId] = new ParticipantInfo
            {
                ConnectionId = Context.ConnectionId,
                UserName = userName,
                JoinedAt = DateTime.UtcNow
            };
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        // Notify others in room
        await Clients.OthersInGroup(roomName).SendAsync("UserJoined", Context.ConnectionId, userName);

        // Send existing participants to new user
        var otherParticipants = roomInfo.Participants
            .Where(p => p.Key != Context.ConnectionId)
            .Select(p => new { connectionId = p.Key, userName = p.Value.UserName })
            .ToList();

        await Clients.Caller.SendAsync("ExistingParticipants", otherParticipants);
    }

    public async Task SendSignal(string signal, string targetConnectionId)
    {
        _logger.LogDebug("Sending signal from {From} to {To}", Context.ConnectionId, targetConnectionId);
        await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", signal, Context.ConnectionId);
    }

    public async Task SendOffer(string offer, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", offer, Context.ConnectionId);
    }

    public async Task SendAnswer(string answer, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", answer, Context.ConnectionId);
    }

    public async Task SendIceCandidate(string candidate, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", candidate, Context.ConnectionId);
    }
}
```

### WebRTC Client
```javascript
// webrtc-client.js
class WebRTCClient {
    constructor(signalRConnection) {
        this.signalR = signalRConnection;
        this.peers = new Map();
        this.localStream = null;
        this.configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'turn:turnserver.com:3478', username: 'user', credential: 'pass' }
            ]
        };
    }

    async initializeMedia() {
        try {
            this.localStream = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: true
            });
            return this.localStream;
        } catch (error) {
            console.error('Failed to get user media:', error);
            throw error;
        }
    }

    async createPeerConnection(targetConnectionId, isOffer) {
        const pc = new RTCPeerConnection(this.configuration);
        
        // Add local stream tracks
        this.localStream.getTracks().forEach(track => {
            pc.addTrack(track, this.localStream);
        });

        // Handle incoming tracks
        pc.ontrack = (event) => {
            console.log('Received remote track from', targetConnectionId);
            this.onRemoteStream(targetConnectionId, event.streams[0]);
        };

        // Handle ICE candidates
        pc.onicecandidate = (event) => {
            if (event.candidate) {
                this.signalR.invoke('SendIceCandidate', 
                    JSON.stringify(event.candidate), 
                    targetConnectionId
                );
            }
        };

        // Handle connection state changes
        pc.onconnectionstatechange = () => {
            console.log(`Connection state with ${targetConnectionId}: ${pc.connectionState}`);
            if (pc.connectionState === 'failed' || pc.connectionState === 'disconnected') {
                this.handlePeerDisconnected(targetConnectionId);
            }
        };

        this.peers.set(targetConnectionId, pc);

        if (isOffer) {
            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            await this.signalR.invoke('SendOffer', JSON.stringify(offer), targetConnectionId);
        }

        return pc;
    }

    async handleOffer(offer, fromConnectionId) {
        const pc = await this.createPeerConnection(fromConnectionId, false);
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
        
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        await this.signalR.invoke('SendAnswer', JSON.stringify(answer), fromConnectionId);
    }

    async handleAnswer(answer, fromConnectionId) {
        const pc = this.peers.get(fromConnectionId);
        if (pc) {
            await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(answer)));
        }
    }

    async handleIceCandidate(candidate, fromConnectionId) {
        const pc = this.peers.get(fromConnectionId);
        if (pc) {
            await pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)));
        }
    }

    disconnect() {
        this.peers.forEach((pc, connectionId) => {
            pc.close();
        });
        this.peers.clear();
        
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
        }
    }
}
```

## 4. MQTT IoT Integration

### MQTT Service
```csharp
// Mqtt/MqttService.cs
public class MqttService : IMqttService, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _options;
    private readonly ILogger<MqttService> _logger;
    private readonly ConcurrentDictionary<string, Func<MqttApplicationMessage, Task>> _handlers = new();

    public MqttService(IConfiguration configuration, ILogger<MqttService> logger)
    {
        _logger = logger;
        
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(configuration["Mqtt:Server"], int.Parse(configuration["Mqtt:Port"]))
            .WithClientId($"puzzle-server-{Guid.NewGuid()}")
            .WithCleanSession()
            .Build();

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            _logger.LogDebug("MQTT message received on topic: {Topic}", topic);

            foreach (var handler in _handlers)
            {
                if (IsTopicMatch(handler.Key, topic))
                {
                    await handler.Value(e.ApplicationMessage);
                }
            }
        };

        _mqttClient.ConnectedAsync += async e =>
        {
            _logger.LogInformation("Connected to MQTT broker");
            
            // Subscribe to IoT device topics
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic("iot/+/sensor/+")
                .Build());
                
            await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                .WithTopic("iot/+/status")
                .Build());
        };
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _mqttClient.ConnectAsync(_options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            throw;
        }
    }

    public async Task PublishAsync(string topic, object payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttClient.PublishAsync(message);
    }

    public void Subscribe(string topicPattern, Func<MqttApplicationMessage, Task> handler)
    {
        _handlers[topicPattern] = handler;
    }

    private bool IsTopicMatch(string pattern, string topic)
    {
        var patternParts = pattern.Split('/');
        var topicParts = topic.Split('/');
        
        if (patternParts.Length != topicParts.Length && !pattern.EndsWith("#"))
            return false;
            
        for (int i = 0; i < patternParts.Length; i++)
        {
            if (patternParts[i] == "#")
                return true;
                
            if (patternParts[i] == "+")
                continue;
                
            if (i >= topicParts.Length || patternParts[i] != topicParts[i])
                return false;
        }
        
        return true;
    }

    public void Dispose()
    {
        _mqttClient?.DisconnectAsync().Wait();
        _mqttClient?.Dispose();
    }
}
```

### IoT Device Simulator
```csharp
// Mqtt/IoTDeviceSimulator.cs
public class IoTDeviceSimulator : BackgroundService
{
    private readonly IMqttService _mqttService;
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _mqttService.ConnectAsync();
        
        // Simulate multiple IoT devices
        var deviceTasks = new[]
        {
            SimulateTableSensors(stoppingToken),
            SimulateEnvironmentalSensors(stoppingToken),
            SimulateSmartPuzzleBox(stoppingToken)
        };
        
        await Task.WhenAll(deviceTasks);
    }

    private async Task SimulateTableSensors(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var sensorData = new
            {
                deviceId = "table-sensor-01",
                timestamp = DateTime.UtcNow,
                pressure = new
                {
                    zones = GeneratePressureZones(),
                    totalWeight = _random.Next(0, 500)
                },
                touch = new
                {
                    points = GenerateTouchPoints(),
                    gestures = DetectGestures()
                }
            };
            
            await _mqttService.PublishAsync("iot/table-01/sensor/pressure", sensorData);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task SimulateEnvironmentalSensors(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envData = new
            {
                deviceId = "env-sensor-01",
                timestamp = DateTime.UtcNow,
                temperature = 20 + _random.NextDouble() * 5,
                humidity = 40 + _random.NextDouble() * 20,
                lightLevel = _random.Next(100, 1000),
                soundLevel = _random.Next(30, 70)
            };
            
            await _mqttService.PublishAsync("iot/room-01/sensor/environment", envData);
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }
}
```

## 5. Socket.IO Compatibility

### Socket.IO Middleware
```csharp
// SocketIO/SocketIOMiddleware.cs
public class SocketIOMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SocketIOMiddleware> _logger;
    private readonly SocketIOProtocolHandler _protocolHandler;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/socket.io"))
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await HandleSocketIOConnection(webSocket, context);
            }
            else if (context.Request.Method == "GET" && context.Request.Query["transport"] == "polling")
            {
                await HandlePollingRequest(context);
            }
            else
            {
                await _next(context);
            }
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

        // Send Socket.IO handshake
        await SendSocketIOMessage(webSocket, "0{\"sid\":\"" + connectionId + "\",\"upgrades\":[],\"pingInterval\":25000,\"pingTimeout\":60000}");

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    await ProcessSocketIOMessage(webSocket, connectionId, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Socket.IO connection error");
        }
    }

    private async Task ProcessSocketIOMessage(WebSocket webSocket, string connectionId, string message)
    {
        var (type, eventName, data) = _protocolHandler.ParseMessage(message);
        
        switch (type)
        {
            case SocketIOMessageType.Connect:
                await SendSocketIOMessage(webSocket, "40");
                break;
                
            case SocketIOMessageType.Event:
                await HandleSocketIOEvent(webSocket, connectionId, eventName, data);
                break;
                
            case SocketIOMessageType.Ping:
                await SendSocketIOMessage(webSocket, "3");
                break;
        }
    }

    private async Task HandleSocketIOEvent(WebSocket webSocket, string connectionId, string eventName, object data)
    {
        // Map Socket.IO events to SignalR hub methods
        switch (eventName)
        {
            case "join":
                // Forward to SignalR hub
                break;
                
            case "move_piece":
                // Forward to SignalR hub
                break;
                
            case "chat_message":
                // Forward to SignalR hub
                break;
        }
        
        // Send acknowledgment
        await SendSocketIOMessage(webSocket, $"42[\"{eventName}_ack\",{{\"success\":true}}]");
    }

    private async Task SendSocketIOMessage(WebSocket webSocket, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
```

## 6. Minimal API Endpoints

### Health Endpoints
```csharp
// MinimalApis/HealthEndpoints.cs
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Basic health check
        endpoints.MapGet("/health", () => Results.Ok(new { 
            status = "Healthy", 
            timestamp = DateTime.UtcNow 
        }))
        .WithName("HealthCheck")
        .WithSummary("Basic health check")
        .WithTags("Health")
        .Produces<object>(StatusCodes.Status200OK)
        .AllowAnonymous();

        // Detailed health check
        endpoints.MapHealthChecks("/health/detailed", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse,
            AllowCachingResponses = false
        })
        .WithName("DetailedHealthCheck")
        .WithSummary("Detailed health check")
        .WithTags("Health")
        .AllowAnonymous();

        // Kubernetes probes
        endpoints.MapGet("/health/live", () => Results.Ok(new { 
            status = "Alive", 
            timestamp = DateTime.UtcNow 
        }))
        .ExcludeFromDescription();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        })
        .ExcludeFromDescription();
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            timestamp = DateTime.UtcNow,
            results = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration,
                    exception = entry.Value.Exception?.Message,
                    data = entry.Value.Data
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
```

### Demo Endpoints
```csharp
// MinimalApis/DemoEndpoints.cs
public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/demo")
            .WithTags("Demo")
            .WithOpenApi()
            .RequireRateLimiting("fixed");

        // API Status
        group.MapGet("/status", () =>
        {
            return Results.Ok(new
            {
                status = "running",
                version = "1.0.0",
                timestamp = DateTime.UtcNow,
                features = new[]
                {
                    "SignalR Hub",
                    "WebSocket Raw",
                    "WebRTC Signaling",
                    "MQTT Integration",
                    "Socket.IO Compatibility",
                    "Kubernetes Ready"
                }
            });
        })
        .WithName("GetApiStatus")
        .WithSummary("Get API status")
        .Produces<object>(StatusCodes.Status200OK);

        // Connection Endpoints
        group.MapGet("/connections", () =>
        {
            return Results.Ok(new
            {
                endpoints = new[]
                {
                    new { type = "SignalR", url = "/puzzlehub", protocol = "WebSocket" },
                    new { type = "WebRTC", url = "/webrtchub", protocol = "WebSocket" },
                    new { type = "Raw WebSocket", url = "/ws", protocol = "WebSocket" },
                    new { type = "Socket.IO", url = "/socket.io", protocol = "WebSocket" },
                    new { type = "MQTT", url = "ws://localhost:9001", protocol = "MQTT over WebSocket" }
                }
            });
        })
        .WithName("GetConnectionEndpoints")
        .WithSummary("Get available connection endpoints")
        .Produces<object>(StatusCodes.Status200OK);

        // Echo endpoint
        group.MapPost("/echo", (EchoRequest request) =>
        {
            return Results.Ok(new
            {
                message = request.Message,
                timestamp = DateTime.UtcNow,
                echoedAt = DateTime.UtcNow.ToString("O")
            });
        })
        .WithName("EchoMessage")
        .WithSummary("Echo a message")
        .Produces<object>(StatusCodes.Status200OK);
    }
}
```

## 7. Repository Pattern

### Piece Repository
```csharp
// Infrastructure/Repositories/PieceRepository.cs
public class PieceRepository : IPieceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PieceRepository> _logger;

    public PieceRepository(string connectionString, ILogger<PieceRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<PuzzlePiece?> GetPieceByIdAsync(Guid pieceId)
    {
        using var connection = new SqlConnection(_connectionString);
        var parameters = new DynamicParameters();
        parameters.Add("@PieceId", pieceId, DbType.Guid);

        var piece = await connection.QueryFirstOrDefaultAsync<PuzzlePiece>(
            "sp_GetPieceById",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return piece;
    }

    public async Task<PieceMoveResult> UpdatePiecePositionAsync(Guid pieceId, int x, int y, int rotation, bool isPlaced)
    {
        using var connection = new SqlConnection(_connectionString);
        var parameters = new DynamicParameters();
        parameters.Add("@PieceId", pieceId, DbType.Guid);
        parameters.Add("@X", x, DbType.Int32);
        parameters.Add("@Y", y, DbType.Int32);
        parameters.Add("@Rotation", rotation, DbType.Int32);
        parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@IsPlaced", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@CompletedPieces", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@CompletionPercentage", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("@PuzzleCompleted", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "sp_UpdatePiecePosition",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return new PieceMoveResult
        {
            Success = parameters.Get<bool>("@Success"),
            IsPlaced = parameters.Get<bool>("@IsPlaced"),
            CompletedPieces = parameters.Get<int>("@CompletedPieces"),
            CompletionPercentage = parameters.Get<decimal>("@CompletionPercentage"),
            PuzzleCompleted = parameters.Get<bool>("@PuzzleCompleted")
        };
    }

    public async Task<bool> LockPieceAsync(Guid pieceId, Guid userId)
    {
        using var connection = new SqlConnection(_connectionString);
        var parameters = new DynamicParameters();
        parameters.Add("@PieceId", pieceId, DbType.Guid);
        parameters.Add("@UserId", userId, DbType.Guid);
        parameters.Add("@LockDurationMinutes", 5, DbType.Int32);
        parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "sp_LockPiece",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return parameters.Get<bool>("@Success");
    }
}
```

## 8. Redis Caching

### Redis Service
```csharp
// Infrastructure/Services/RedisService.cs
public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = _connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from Redis for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in Redis for key: {Key}", key);
        }
    }

    public async Task<bool> TryAcquireLockAsync(string key, string value, TimeSpan expiry)
    {
        return await _database.StringSetAsync(key, value, expiry, When.NotExists);
    }

    public async Task<bool> ReleaseLockAsync(string key, string value)
    {
        var lua = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await _database.ScriptEvaluateAsync(lua, new RedisKey[] { key }, new RedisValue[] { value });
        return (int)result == 1;
    }

    public async Task AddToListAsync<T>(string key, T value, int maxLength = 1000) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await _database.ListLeftPushAsync(key, json);
        await _database.ListTrimAsync(key, 0, maxLength - 1);
    }

    public async Task<IEnumerable<T>> GetListAsync<T>(string key, int count = 100) where T : class
    {
        var values = await _database.ListRangeAsync(key, 0, count - 1);
        return values.Select(v => JsonSerializer.Deserialize<T>(v!)).Where(v => v != null);
    }

    public async Task PublishAsync(string channel, object message)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        var json = JsonSerializer.Serialize(message);
        await subscriber.PublishAsync(channel, json);
    }

    public async Task SubscribeAsync(string channel, Action<string> handler)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(channel, (ch, message) =>
        {
            handler(message!);
        });
    }
}
```

## 9. Health Checks

### Custom Health Checks
```csharp
// HealthChecks/RedisHealthCheck.cs
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            await database.PingAsync();

            var endpoints = _connectionMultiplexer.GetEndPoints();
            var server = _connectionMultiplexer.GetServer(endpoints.First());
            var info = await server.InfoAsync();

            var connectedClients = info
                .FirstOrDefault(s => s.Key == "Clients")?
                .FirstOrDefault(i => i.Key == "connected_clients")
                .Value ?? "0";

            return HealthCheckResult.Healthy("Redis is healthy", new Dictionary<string, object>
            {
                ["connected_clients"] = connectedClients,
                ["endpoints"] = endpoints.Length
            });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unhealthy", ex);
        }
    }
}

// HealthChecks/SignalRHealthCheck.cs
public class SignalRHealthCheck : IHealthCheck
{
    private readonly IHubContext<PuzzleHub> _hubContext;

    public SignalRHealthCheck(IHubContext<PuzzleHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can access the hub context
            var clients = _hubContext.Clients;
            if (clients != null)
            {
                return Task.FromResult(HealthCheckResult.Healthy("SignalR is healthy"));
            }

            return Task.FromResult(HealthCheckResult.Unhealthy("SignalR clients not accessible"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("SignalR check failed", ex));
        }
    }
}
```

## 10. Security Implementation

### JWT Authentication Service
```csharp
// Services/JwtService.cs
public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _key = Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]);
    }

    public string GenerateToken(User user, IEnumerable<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
```

### Rate Limiting
```csharp
// Program.cs - Rate Limiting Configuration
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.AutoReplenishment = true;
    });

    options.AddSlidingWindowLimiter("sliding", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 6;
    });

    options.AddTokenBucketLimiter("token", limiterOptions =>
    {
        limiterOptions.TokenLimit = 100;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        limiterOptions.TokensPerPeriod = 20;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Try again later.", token);
    };
});
```

## 11. Kubernetes Configuration

### API Deployment
```yaml
# k8s/base/api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: puzzle-api
  namespace: puzzle-platform
spec:
  replicas: 3
  selector:
    matchLabels:
      app: puzzle-api
  template:
    metadata:
      labels:
        app: puzzle-api
    spec:
      containers:
      - name: api
        image: puzzleplatform/api:latest
        ports:
        - containerPort: 80
          name: http
        - containerPort: 443
          name: https
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: puzzle-secrets
              key: db-connection
        - name: ConnectionStrings__Redis
          value: "puzzle-redis-service:6379"
        resources:
          limits:
            memory: "512Mi"
            cpu: "1000m"
          requests:
            memory: "256Mi"
            cpu: "200m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: puzzle-api-service
  namespace: puzzle-platform
spec:
  selector:
    app: puzzle-api
  ports:
  - port: 80
    targetPort: 80
    name: http
  - port: 443
    targetPort: 443
    name: https
  type: ClusterIP
```

### Horizontal Pod Autoscaler
```yaml
# k8s/base/hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: puzzle-api-hpa
  namespace: puzzle-platform
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: puzzle-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: signalr_connections_per_pod
      target:
        type: AverageValue
        averageValue: "1000"
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 100
        periodSeconds: 15
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 15
      - type: Pods
        value: 4
        periodSeconds: 15
      selectPolicy: Max
```

## 12. Docker Setup

### Multi-stage Dockerfile
```dockerfile
# src/CollaborativePuzzle.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj", "src/CollaborativePuzzle.Api/"]
COPY ["src/CollaborativePuzzle.Core/CollaborativePuzzle.Core.csproj", "src/CollaborativePuzzle.Core/"]
COPY ["src/CollaborativePuzzle.Infrastructure/CollaborativePuzzle.Infrastructure.csproj", "src/CollaborativePuzzle.Infrastructure/"]
COPY ["src/CollaborativePuzzle.Hubs/CollaborativePuzzle.Hubs.csproj", "src/CollaborativePuzzle.Hubs/"]
RUN dotnet restore "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj"

# Copy everything and build
COPY . .
WORKDIR "/src/src/CollaborativePuzzle.Api"
RUN dotnet build "CollaborativePuzzle.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CollaborativePuzzle.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Add health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]
```

### Docker Compose
```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: src/CollaborativePuzzle.Api/Dockerfile
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=https://+:443;http://+:80
      - ASPNETCORE_Kestrel__Certificates__Default__Password=password
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=CollaborativePuzzle;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;
      - ConnectionStrings__Redis=redis:6379
      - Mqtt__Server=mosquitto
      - Mqtt__Port=1883
    volumes:
      - ~/.aspnet/https:/https:ro
    depends_on:
      - sqlserver
      - redis
      - mosquitto
    networks:
      - puzzle-network

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    networks:
      - puzzle-network

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - puzzle-network

  mosquitto:
    image: eclipse-mosquitto:2
    ports:
      - "1883:1883"
      - "9001:9001"
    volumes:
      - ./docker/mosquitto/config:/mosquitto/config
      - mosquitto-data:/mosquitto/data
      - mosquitto-log:/mosquitto/log
    networks:
      - puzzle-network

volumes:
  sqlserver-data:
  redis-data:
  mosquitto-data:
  mosquitto-log:

networks:
  puzzle-network:
    driver: bridge
```

## Summary

This comprehensive code sample collection demonstrates:

1. **Real-time Communication**: SignalR hubs with typed clients and Redis backplane
2. **WebSocket Raw**: Direct WebSocket handling for custom protocols
3. **WebRTC**: Signaling server for peer-to-peer connections
4. **MQTT**: IoT device integration with topic-based messaging
5. **Socket.IO**: Compatibility layer for legacy clients
6. **Minimal APIs**: Modern REST endpoints with OpenAPI
7. **Repository Pattern**: Clean data access with Dapper
8. **Redis Caching**: Distributed caching and locking
9. **Health Checks**: Comprehensive health monitoring
10. **Security**: JWT authentication and rate limiting
11. **Kubernetes**: Production-ready deployment manifests
12. **Docker**: Multi-stage builds and compose setup

Each example follows enterprise best practices and can be adapted for production use. The code demonstrates scalability, maintainability, and modern .NET development patterns.