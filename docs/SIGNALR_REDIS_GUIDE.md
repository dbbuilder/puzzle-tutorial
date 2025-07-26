# SignalR with Redis Backplane Implementation Guide

## Overview

This guide demonstrates a production-ready implementation of SignalR with Redis backplane for real-time collaborative features. The implementation showcases distributed locking, message throttling, connection tracking, and scalable pub/sub patterns.

## Architecture

### Core Components

1. **PuzzleHub**: Main SignalR hub handling real-time communication
2. **Redis Backplane**: Enables horizontal scaling across multiple servers
3. **Distributed Locking**: Prevents race conditions in multi-server environments
4. **Connection Tracking**: Manages user sessions across disconnections
5. **Message Throttling**: Optimizes high-frequency updates (cursor movements)

### Technology Stack

- **ASP.NET Core SignalR**: Real-time bidirectional communication
- **StackExchange.Redis**: High-performance Redis client
- **MessagePack**: Efficient binary serialization protocol
- **Redis Pub/Sub**: Cross-server message distribution

## Key Features Implemented

### 1. Session Management

```csharp
public async Task<JoinSessionResult> JoinPuzzleSession(string sessionId)
{
    // Validate session
    var session = await _sessionRepository.GetSessionAsync(sessionGuid);
    
    // Add to SignalR group
    await Groups.AddToGroupAsync(Context.ConnectionId, $"puzzle-{sessionId}");
    
    // Track in Redis for resilience
    await _redisService.SetAsync($"connection:{Context.ConnectionId}", 
        new { SessionId = sessionId, UserId = userId }, 
        TimeSpan.FromMinutes(30));
    
    // Notify others
    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserJoined", notification);
}
```

### 2. Distributed Piece Locking

Prevents multiple users from editing the same piece simultaneously:

```csharp
public async Task<LockPieceResult> LockPiece(string pieceId)
{
    // Try distributed lock via Redis
    var lockAcquired = await _redisService.SetAsync(
        $"piece-lock:{pieceId}", 
        userId.ToString(), 
        TimeSpan.FromSeconds(30), 
        When.NotExists);
    
    if (!lockAcquired)
    {
        return new LockPieceResult
        {
            Success = false,
            Error = "Piece is already locked"
        };
    }
    
    // Update database
    await _pieceRepository.LockPieceAsync(pieceGuid, userId);
    
    // Notify others
    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceLocked", notification);
}
```

### 3. Cursor Throttling

Optimizes high-frequency cursor updates using channels:

```csharp
public async Task UpdateCursor(double x, double y)
{
    // Get or create throttling channel
    var channel = _cursorChannels.GetOrAdd(Context.ConnectionId, _ =>
    {
        var ch = Channel.CreateUnbounded<CursorUpdateNotification>();
        _ = ProcessCursorUpdates(ch.Reader, sessionId);
        return ch;
    });
    
    // Queue update (non-blocking)
    await channel.Writer.WriteAsync(new CursorUpdateNotification
    {
        UserId = userId,
        X = x,
        Y = y
    });
}

private async Task ProcessCursorUpdates(ChannelReader<CursorUpdateNotification> reader, string sessionId)
{
    var lastUpdate = DateTime.UtcNow;
    CursorUpdateNotification? latestUpdate = null;
    
    await foreach (var update in reader.ReadAllAsync())
    {
        latestUpdate = update;
        
        // Throttle to 10 updates per second
        if (DateTime.UtcNow - lastUpdate >= TimeSpan.FromMilliseconds(100))
        {
            await _redisService.PublishAsync($"cursor:{sessionId}", latestUpdate);
            await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("CursorUpdate", latestUpdate);
            
            lastUpdate = DateTime.UtcNow;
            latestUpdate = null;
        }
    }
}
```

### 4. Connection Resilience

Handles disconnections gracefully:

```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    // Get session from Redis
    var sessionId = await _redisService.GetAsync<string>($"connection:{Context.ConnectionId}:session");
    
    if (!string.IsNullOrEmpty(sessionId))
    {
        // Clean up user state
        await _sessionRepository.RemoveParticipantAsync(sessionGuid, userId);
        await _pieceRepository.UnlockAllPiecesForUserAsync(userId);
        
        // Notify others
        await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserLeft", notification);
    }
    
    // Clean up Redis tracking
    await _redisService.DeleteAsync($"connection:{Context.ConnectionId}");
    await _redisService.DeleteAsync($"user:{userId}:session");
}
```

## Configuration

### Startup Configuration

```csharp
// Program.cs
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
})
.AddMessagePackProtocol() // Binary serialization for performance
.AddStackExchangeRedis(redisConnectionString, options =>
{
    options.Configuration.ChannelPrefix = "puzzle-app";
});

// Add Redis service
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IRedisService, RedisService>();

// Map hub endpoint
app.MapHub<PuzzleHub>("/puzzlehub");
```

### Redis Connection String Examples

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=yourpassword,ssl=false,abortConnect=false"
  }
}
```

For Azure Redis Cache:
```json
{
  "ConnectionStrings": {
    "Redis": "your-cache.redis.cache.windows.net:6380,password=yourkey,ssl=true,abortConnect=false"
  }
}
```

## Client-Side Implementation

### TypeScript/JavaScript Client

```typescript
import * as signalR from "@microsoft/signalr";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";

class PuzzleClient {
    private connection: signalR.HubConnection;
    
    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/puzzlehub")
            .withHubProtocol(new MessagePackHubProtocol())
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();
        
        this.setupEventHandlers();
    }
    
    private setupEventHandlers(): void {
        // User events
        this.connection.on("UserJoined", (notification) => {
            console.log(`${notification.displayName} joined the session`);
        });
        
        this.connection.on("UserLeft", (notification) => {
            console.log(`User ${notification.userId} left the session`);
        });
        
        // Piece events
        this.connection.on("PieceMoved", (notification) => {
            this.updatePiecePosition(notification.pieceId, notification.x, notification.y, notification.rotation);
        });
        
        this.connection.on("PieceLocked", (notification) => {
            this.markPieceLocked(notification.pieceId, notification.lockedByUserId);
        });
        
        // Chat events
        this.connection.on("ChatMessage", (notification) => {
            this.displayChatMessage(notification);
        });
        
        // Cursor updates (high frequency)
        this.connection.on("CursorUpdate", (notification) => {
            this.updateUserCursor(notification.userId, notification.x, notification.y);
        });
        
        // Completion event
        this.connection.on("PuzzleCompleted", (notification) => {
            this.celebrateCompletion(notification);
        });
    }
    
    public async start(): Promise<void> {
        try {
            await this.connection.start();
            console.log("SignalR Connected");
        } catch (err) {
            console.error("SignalR Connection Error: ", err);
            setTimeout(() => this.start(), 5000);
        }
    }
    
    public async joinSession(sessionId: string): Promise<void> {
        const result = await this.connection.invoke("JoinPuzzleSession", sessionId);
        if (result.success) {
            this.loadSessionState(result.sessionState);
        } else {
            throw new Error(result.error);
        }
    }
    
    public async movePiece(pieceId: string, x: number, y: number, rotation: number): Promise<void> {
        const result = await this.connection.invoke("MovePiece", pieceId, x, y, rotation);
        if (!result.success) {
            console.error("Failed to move piece:", result.error);
        }
    }
    
    public async lockPiece(pieceId: string): Promise<boolean> {
        const result = await this.connection.invoke("LockPiece", pieceId);
        return result.success;
    }
    
    public sendCursorUpdate(x: number, y: number): void {
        // Fire and forget for performance
        this.connection.send("UpdateCursor", x, y);
    }
}
```

## Scaling Considerations

### 1. Redis Clustering

For high availability:

```csharp
// Use Redis Sentinel
var sentinelConnection = "sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=mymaster";

// Or Redis Cluster
var clusterConnection = "node1:6379,node2:6379,node3:6379";
```

### 2. Message Size Optimization

Use MessagePack for binary serialization:

```csharp
.AddMessagePackProtocol(options =>
{
    options.SerializerOptions = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray)
        .WithSecurity(MessagePackSecurity.UntrustedData);
});
```

### 3. Connection Limits

Configure limits based on server capacity:

```csharp
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.StreamBufferCapacity = 10;
    options.MaximumParallelInvocationsPerClient = 1;
});
```

## Performance Monitoring

### 1. SignalR Metrics

```csharp
public class SignalRMetrics
{
    private readonly IMetrics _metrics;
    
    public void RecordHubMethodDuration(string methodName, double duration)
    {
        _metrics.Measure.Timer.Time("signalr.hub.method.duration", 
            new MetricTags("method", methodName), 
            (long)duration);
    }
    
    public void RecordActiveConnections(int count)
    {
        _metrics.Measure.Gauge.SetValue("signalr.connections.active", count);
    }
}
```

### 2. Redis Monitoring

Monitor key metrics:
- Connection pool usage
- Command execution time
- Pub/Sub message latency
- Memory usage

### 3. Application Insights Integration

```csharp
builder.Services.AddApplicationInsightsTelemetry();

// Custom telemetry
public class HubTelemetry : Hub
{
    private readonly TelemetryClient _telemetryClient;
    
    public override async Task OnConnectedAsync()
    {
        _telemetryClient.TrackEvent("SignalR.Connected", new Dictionary<string, string>
        {
            ["UserId"] = Context.UserIdentifier,
            ["ConnectionId"] = Context.ConnectionId
        });
    }
}
```

## Security Best Practices

### 1. Authentication

Always require authentication:

```csharp
[Authorize]
public class PuzzleHub : Hub
{
    // Hub is only accessible to authenticated users
}
```

### 2. Authorization

Implement method-level authorization:

```csharp
public async Task<JoinSessionResult> JoinPuzzleSession(string sessionId)
{
    // Verify user has permission to join this session
    var hasAccess = await _authorizationService.AuthorizeAsync(
        Context.User, sessionId, "CanJoinSession");
    
    if (!hasAccess.Succeeded)
    {
        return HubResult.CreateError<JoinSessionResult>("Access denied");
    }
}
```

### 3. Input Validation

Always validate client input:

```csharp
public async Task<MovePieceResult> MovePiece(string pieceId, double x, double y, int rotation)
{
    // Validate piece ID format
    if (!Guid.TryParse(pieceId, out var pieceGuid))
    {
        return HubResult.CreateError<MovePieceResult>("Invalid piece ID format");
    }
    
    // Validate coordinates
    if (x < 0 || y < 0 || x > MAX_CANVAS_WIDTH || y > MAX_CANVAS_HEIGHT)
    {
        return HubResult.CreateError<MovePieceResult>("Invalid coordinates");
    }
    
    // Validate rotation
    if (rotation % 90 != 0 || rotation < 0 || rotation >= 360)
    {
        return HubResult.CreateError<MovePieceResult>("Invalid rotation");
    }
}
```

### 4. Rate Limiting

Protect against abuse:

```csharp
[RateLimit("MovePiece", PermitLimit = 60, Window = 60)] // 60 moves per minute
public async Task<MovePieceResult> MovePiece(string pieceId, double x, double y, int rotation)
{
    // Implementation
}
```

## Troubleshooting

### Common Issues

1. **Connection Drops**
   - Check keep-alive settings
   - Verify firewall rules
   - Monitor Redis connectivity

2. **Missing Messages**
   - Ensure Redis backplane is configured
   - Check group membership
   - Verify serialization settings

3. **Performance Issues**
   - Monitor message size
   - Check Redis latency
   - Review throttling logic

### Debug Logging

Enable detailed logging:

```csharp
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddSignalRHub<PuzzleHub>("puzzlehub", tags: new[] { "live" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "ready" });
```

## Summary

This implementation demonstrates:
- ✅ Scalable real-time communication with SignalR
- ✅ Redis backplane for horizontal scaling
- ✅ Distributed locking for data consistency
- ✅ Message throttling for performance
- ✅ Connection resilience and tracking
- ✅ Security best practices
- ✅ Performance optimization techniques

The architecture supports thousands of concurrent users across multiple servers while maintaining sub-second latency for real-time updates.