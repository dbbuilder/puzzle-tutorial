# SignalR Implementation Guide

## Overview
SignalR is a library for ASP.NET Core that enables real-time web functionality. It allows server-side code to push content to clients instantly, making it perfect for collaborative puzzle solving where multiple users need to see piece movements in real-time.

## Purpose in Our Application
- **Real-time puzzle piece synchronization** across all connected users
- **Live user presence indicators** showing who is online and where they're working
- **Instant chat messaging** within puzzle sessions
- **Progress updates** when pieces are correctly placed
- **Collaborative cursor tracking** to show where other users are working

## Architecture Implementation

### 1. Hub Setup
SignalR uses "Hubs" as the server-side component that manages connections and broadcasts messages.

**File**: `src/CollaborativePuzzle.Hubs/PuzzleHub.cs`

```csharp
public class PuzzleHub : Hub
{
    // Handles client connections and method calls
    // Manages user groups (puzzle sessions)
    // Broadcasts updates to relevant users
}
```

### 2. Redis Backplane Configuration
For horizontal scaling across multiple server instances, we use Redis as a backplane.

**Configuration in Program.cs**:
```csharp
services.AddSignalR()
    .AddMessagePackProtocol()  // For efficient serialization
    .AddStackExchangeRedis(connectionString);  // Redis backplane
```

### 3. Message Flow Architecture
```
Client 1 --> SignalR Hub --> Redis Backplane --> All Server Instances --> All Connected Clients
Client 2 --> SignalR Hub --> Redis Backplane --> All Server Instances --> All Connected Clients
```

## Key Features Implementation

### Real-time Piece Movement
When a user moves a puzzle piece:
1. Client sends `MovePiece` message to hub
2. Hub validates the move and updates database
3. Hub broadcasts `PieceMoved` to all users in the session
4. All clients update their UI instantly

### Connection Management
- Users join session groups when connecting to a puzzle
- Automatic cleanup when users disconnect
- Heartbeat monitoring to detect connection issues

### Message Serialization
We use MessagePack for efficient binary serialization:
- Smaller message sizes than JSON
- Faster serialization/deserialization
- Better performance for high-frequency updates

## Scaling Considerations

### Redis Backplane Benefits
- **Horizontal Scaling**: Multiple server instances can handle connections
- **Session Persistence**: Users can reconnect to different servers
- **Load Distribution**: Distribute connection load across servers

### Performance Optimizations
- **Connection Pooling**: Reuse database connections
- **Message Batching**: Group related updates
- **Selective Broadcasting**: Only send updates to relevant users

## Security Implementation
- **Authentication**: JWT token validation for all connections
- **Authorization**: Role-based access to different hub methods
- **Rate Limiting**: Prevent spam and abuse
- **Input Validation**: Sanitize all incoming messages

## Monitoring and Diagnostics
- **Connection Metrics**: Track active connections per server
- **Message Throughput**: Monitor messages per second
- **Error Tracking**: Log connection failures and message errors
- **Performance Counters**: CPU and memory usage during peak loads

## Best Practices

### 1. Hub Method Design
- Keep hub methods lightweight
- Use async/await for all operations
- Return meaningful responses to callers

### 2. Group Management
- Use consistent naming for session groups
- Clean up groups when sessions end
- Implement proper join/leave mechanics

### 3. Error Handling
- Graceful degradation when Redis is unavailable
- Retry mechanisms for transient failures
- Client-side reconnection logic

## Integration with Other Technologies

### WebSocket Fallback
SignalR automatically negotiates the best transport:
1. **WebSockets** (preferred for modern browsers)
2. **Server-Sent Events** (fallback for older browsers)
3. **Long Polling** (universal fallback)

### MQTT Integration
SignalR can work alongside MQTT for IoT scenarios:
- MQTT for device communication
- SignalR for web client communication
- Bridge services to coordinate between protocols

## Testing Strategy

### Unit Testing
- Mock hub contexts for testing business logic
- Test message serialization/deserialization
- Validate authorization policies

### Integration Testing
- Test with multiple concurrent connections
- Validate Redis backplane functionality
- Performance testing under load

### Load Testing
- Simulate thousands of concurrent users
- Test message broadcasting performance
- Validate server resource usage

## Common Issues and Solutions

### Connection Drops
- Implement automatic reconnection on client
- Use connection state monitoring
- Graceful handling of intermittent connectivity

### Message Ordering
- Use sequence numbers for critical updates
- Implement conflict resolution for simultaneous edits
- Consider eventual consistency patterns

### Memory Leaks
- Properly dispose of connections
- Clean up event handlers
- Monitor memory usage in production
