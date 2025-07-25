# WebSockets Implementation Guide

## Overview
WebSockets provide a persistent, full-duplex communication channel between client and server. Unlike HTTP, WebSockets maintain an open connection, enabling real-time, low-latency communication perfect for high-frequency puzzle piece movements.

## Purpose in Our Application
- **Ultra-low latency piece movements** for smooth user experience
- **Direct peer-to-peer communication** for collaborative features
- **High-frequency cursor tracking** without HTTP overhead
- **Reduced server load** for frequent position updates
- **Complementary to SignalR** for performance-critical operations

## Architecture Implementation

### 1. WebSocket Handler Setup
**File**: `src/CollaborativePuzzle.Api/WebSocketHandlers/PuzzleWebSocketHandler.cs`

```csharp
public class PuzzleWebSocketHandler
{
    // Manages direct WebSocket connections
    // Handles high-frequency piece movement messages
    // Implements binary message protocols for efficiency
}
```

### 2. Connection Management
```csharp
// WebSocket connection lifecycle
WebSocket Accept -> Authentication -> Session Join -> Message Loop -> Cleanup
```

### 3. Message Protocol Design
We implement a custom binary protocol for maximum efficiency:

```
Message Format:
[MessageType:1byte][SessionId:16bytes][UserId:16bytes][Payload:variable]

Message Types:
- 0x01: Piece Movement
- 0x02: Cursor Update  
- 0x03: Piece Lock/Unlock
- 0x04: Heartbeat
```

## Performance Optimizations

### Binary Message Format
Instead of JSON, we use binary encoding:
- **Piece Movement**: 37 bytes vs 150+ bytes JSON
- **Cursor Update**: 25 bytes vs 80+ bytes JSON
- **50-70% bandwidth reduction** for high-frequency messages

### Message Batching
```csharp
// Batch multiple piece movements into single message
BatchMessage {
    MessageType: PieceMovementBatch,
    Movements: [
        { PieceId, X, Y, Rotation },
        { PieceId, X, Y, Rotation },
        // ... up to 10 movements per batch
    ]
}
```

### Connection Pooling
- Reuse WebSocket connections within sessions
- Implement connection heartbeat to detect failures
- Graceful failover to SignalR when WebSocket unavailable

## Integration with SignalR

### Hybrid Approach
We use both technologies for different purposes:

**WebSockets for**:
- High-frequency piece movements (>10 per second)
- Real-time cursor tracking
- Performance-critical updates

**SignalR for**:
- Session management (join/leave)
- Chat messages
- User presence updates
- System notifications

### Message Coordination
```csharp
// Ensure message ordering between WebSocket and SignalR
public class MessageCoordinator
{
    // Synchronize high-frequency WebSocket updates
    // with lower-frequency SignalR messages
    // Prevent race conditions and ensure consistency
}
```

## Security Implementation

### Authentication
```csharp
// WebSocket authentication using JWT tokens
public async Task<bool> AuthenticateWebSocketAsync(HttpContext context)
{
    var token = context.Request.Query["access_token"];
    // Validate JWT token
    // Extract user claims
    // Authorize session access
}
```

### Rate Limiting
```csharp
// Prevent WebSocket message spam
public class WebSocketRateLimiter
{
    // Track messages per second per connection
    // Implement sliding window rate limiting
    // Disconnect abusive connections
}
```

### Message Validation
- Validate all incoming binary messages
- Sanitize piece movement coordinates
- Verify user permissions for each action

## Connection Management

### Heartbeat Protocol
```csharp
// Keep connections alive and detect failures
public async Task StartHeartbeatAsync(WebSocket socket)
{
    while (socket.State == WebSocketState.Open)
    {
        await SendHeartbeatAsync(socket);
        await Task.Delay(30000); // 30 second intervals
    }
}
```

### Graceful Disconnection
```csharp
public async Task HandleDisconnectionAsync(WebSocket socket, Guid userId)
{
    // Clean up user's locked pieces
    // Notify other users of disconnection
    // Release resources
    // Update user presence status
}
```

## Error Handling

### Connection Failures
- Automatic reconnection with exponential backoff
- Fallback to SignalR when WebSocket unavailable
- Message queuing during reconnection attempts

### Message Corruption
```csharp
public bool ValidateMessage(byte[] message)
{
    // Check message length
    // Validate message type
    // Verify checksums if implemented
    // Handle malformed messages gracefully
}
```

## Performance Monitoring

### Metrics Collection
```csharp
// Track WebSocket performance metrics
public class WebSocketMetrics
{
    public int ActiveConnections { get; set; }
    public long MessagesPerSecond { get; set; }
    public double AverageLatency { get; set; }
    public int ReconnectionCount { get; set; }
    public long BytesSentPerSecond { get; set; }
    public long BytesReceivedPerSecond { get; set; }
}
```

### Performance Benchmarks
- **Target Latency**: < 10ms for piece movements
- **Throughput**: 1000+ messages/second per connection
- **Memory Usage**: < 1MB per active connection
- **CPU Overhead**: < 1% per 100 connections

## Client-Side Implementation

### JavaScript WebSocket Client
```javascript
class PuzzleWebSocketClient {
    constructor(sessionId, authToken) {
        this.sessionId = sessionId;
        this.authToken = authToken;
        this.socket = null;
        this.messageQueue = [];
    }
    
    connect() {
        // Establish WebSocket connection
        // Handle authentication
        // Set up message handlers
    }
    
    sendPieceMovement(pieceId, x, y, rotation) {
        // Create binary message
        // Send with error handling
        // Queue if connection unavailable
    }
}
```

### Vue.js Integration
```vue
<template>
  <div @mousemove="updateCursor" @click="movePiece">
    <!-- Puzzle canvas -->
  </div>
</template>

<script>
export default {
  mounted() {
    this.webSocketClient = new PuzzleWebSocketClient(
      this.sessionId, 
      this.authToken
    );
  },
  
  methods: {
    updateCursor(event) {
      // Throttle cursor updates to 60fps
      this.webSocketClient.sendCursorUpdate(event.x, event.y);
    }
  }
}
</script>
```

## Testing Strategy

### Load Testing
```csharp
// Simulate thousands of concurrent WebSocket connections
public async Task WebSocketLoadTest()
{
    var connections = new List<WebSocket>();
    
    // Create 1000 concurrent connections
    for (int i = 0; i < 1000; i++)
    {
        var socket = await CreateWebSocketConnectionAsync();
        connections.Add(socket);
    }
    
    // Send high-frequency messages
    // Measure latency and throughput
    // Monitor server resources
}
```

### Integration Testing
- Test WebSocket + SignalR coordination
- Validate message ordering
- Test reconnection scenarios
- Verify fallback mechanisms

## Common Issues and Solutions

### Browser Compatibility
- **Modern Browsers**: Full WebSocket support
- **Older Browsers**: Automatic fallback to SignalR
- **Mobile Browsers**: Handle background/foreground transitions

### Network Issues
- **Proxy Servers**: May block WebSocket upgrades
- **Corporate Firewalls**: Often block non-HTTP traffic
- **Solution**: Always implement SignalR fallback

### Memory Management
```csharp
// Prevent memory leaks in long-running connections
public void CleanupConnection(WebSocket socket)
{
    socket?.Dispose();
    // Remove from connection tracking
    // Clean up event handlers
    // Release message buffers
}
```

## Best Practices

### 1. Message Design
- Use binary encoding for high-frequency messages
- Implement message versioning for future compatibility
- Keep message sizes small (< 1KB)

### 2. Connection Management
- Implement proper connection lifecycle management
- Use connection pooling where possible
- Monitor connection health continuously

### 3. Scalability
- Design for horizontal scaling from the start
- Use Redis for connection state if needed
- Implement proper load balancing for WebSocket endpoints

### 4. Debugging
- Implement comprehensive logging
- Use performance counters
- Monitor network traffic patterns
- Track user experience metrics
