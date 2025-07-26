# WebSocket Raw Endpoint Protocol Specification

## Overview

The Collaborative Puzzle Platform provides a raw WebSocket endpoint at `/ws` for low-level real-time communication. This endpoint demonstrates WebSocket protocol usage without SignalR abstraction.

## Connection

### Endpoint
```
ws://localhost:5000/ws
wss://yourdomain.com/ws (production)
```

### Connection Flow

1. **Client connects** to the WebSocket endpoint
2. **Server sends welcome message** with connection details
3. **Client and server exchange messages** using the defined protocol
4. **Either party can close** the connection

### Welcome Message

Upon successful connection, the server sends:

```json
{
  "type": "welcome",
  "connectionId": "unique-connection-id",
  "timestamp": "2025-07-26T10:30:00Z",
  "protocols": ["json", "binary"]
}
```

## Message Protocol

### Text Messages (JSON)

All text messages use JSON format with a required `type` field.

#### Ping/Pong

**Client Request:**
```json
{
  "type": "ping"
}
```

**Server Response:**
```json
{
  "type": "pong",
  "timestamp": "2025-07-26T10:30:00Z"
}
```

#### Echo

**Client Request:**
```json
{
  "type": "echo",
  "data": "Any text or JSON data"
}
```

**Server Response:**
```json
{
  "type": "echo",
  "data": "Any text or JSON data",
  "timestamp": "2025-07-26T10:30:00Z"
}
```

#### Broadcast

**Client Request:**
```json
{
  "type": "broadcast",
  "data": {
    "message": "Hello, everyone!",
    "metadata": { "priority": "high" }
  }
}
```

**Server Response:**
```json
{
  "type": "broadcast",
  "data": {
    "message": "Hello, everyone!",
    "metadata": { "priority": "high" }
  },
  "from": "connection-id",
  "timestamp": "2025-07-26T10:30:00Z"
}
```

#### Binary Request

**Client Request:**
```json
{
  "type": "binary-request"
}
```

**Server Response:** Binary message (see Binary Protocol)

#### Error Messages

**Server Response:**
```json
{
  "type": "error",
  "message": "Error description"
}
```

### Binary Messages

Binary messages follow a simple header + payload structure:

```
[4 bytes: payload length as Int32 LE] [N bytes: payload data]
```

#### Binary Echo Response

When the server receives binary data, it echoes it back with a header:

1. **Header (4 bytes)**: Original message length (Little Endian Int32)
2. **Payload**: Original binary data

#### Binary Request Response

When client sends a `binary-request` JSON message:

1. **Header (8 bytes)**: Timestamp as ticks (Little Endian Int64)
2. **Payload (1024 bytes)**: Random binary data

## Error Handling

### Connection Errors

- **400 Bad Request**: Non-WebSocket request to `/ws` endpoint
- **1000 Normal Closure**: Standard close
- **1001 Going Away**: Server shutdown
- **1002 Protocol Error**: Invalid message format
- **1003 Unsupported Data**: Unknown message type
- **1008 Policy Violation**: Message too large
- **1011 Internal Server Error**: Server error

### Message Errors

Invalid or malformed messages receive an error response:

```json
{
  "type": "error",
  "message": "Failed to process message"
}
```

## Implementation Example

### JavaScript Client

```javascript
const socket = new WebSocket('ws://localhost:5000/ws');

// Connection opened
socket.addEventListener('open', (event) => {
    console.log('Connected to WebSocket');
    
    // Send ping
    socket.send(JSON.stringify({ type: 'ping' }));
});

// Listen for messages
socket.addEventListener('message', (event) => {
    if (event.data instanceof Blob) {
        // Handle binary data
        event.data.arrayBuffer().then(buffer => {
            const view = new DataView(buffer);
            const length = view.getInt32(0, true); // Little Endian
            console.log(`Binary message: ${buffer.byteLength} bytes, payload: ${length} bytes`);
        });
    } else {
        // Handle text data
        const data = JSON.parse(event.data);
        console.log('Received:', data);
    }
});

// Send echo message
socket.send(JSON.stringify({
    type: 'echo',
    data: 'Hello, WebSocket!'
}));

// Send binary data
const buffer = new ArrayBuffer(256);
const view = new Uint8Array(buffer);
for (let i = 0; i < 256; i++) {
    view[i] = i;
}
socket.send(buffer);

// Close connection
socket.close(1000, 'Done testing');
```

### C# Client

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var client = new ClientWebSocket();
await client.ConnectAsync(new Uri("ws://localhost:5000/ws"), CancellationToken.None);

// Receive welcome message
var buffer = new ArraySegment<byte>(new byte[4096]);
var result = await client.ReceiveAsync(buffer, CancellationToken.None);
var welcome = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
Console.WriteLine($"Welcome: {welcome}");

// Send ping
var pingMessage = JsonSerializer.Serialize(new { type = "ping" });
await client.SendAsync(
    new ArraySegment<byte>(Encoding.UTF8.GetBytes(pingMessage)),
    WebSocketMessageType.Text,
    true,
    CancellationToken.None);

// Send binary
var binaryData = new byte[] { 1, 2, 3, 4, 5 };
await client.SendAsync(
    new ArraySegment<byte>(binaryData),
    WebSocketMessageType.Binary,
    true,
    CancellationToken.None);

// Close
await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
```

## Performance Considerations

1. **Message Size**: Keep messages under 64KB for optimal performance
2. **Compression**: WebSocket compression is enabled by default
3. **Heartbeat**: Send ping every 30 seconds to keep connection alive
4. **Reconnection**: Implement exponential backoff for reconnection
5. **Binary vs Text**: Use binary for large data transfers

## Security

1. **Authentication**: Implement token-based auth in connection headers
2. **Rate Limiting**: Limit messages per second per connection
3. **Message Validation**: Validate all incoming messages
4. **TLS**: Always use WSS in production
5. **Origin Validation**: Check Origin header in production

## Testing

Use the provided test page at `/websocket-test.html` to interact with the WebSocket endpoint.

### Features:
- Connect/disconnect management
- Send text and binary messages
- Message history with formatting
- Statistics tracking
- Pre-configured message templates

## Comparison with SignalR

| Feature | Raw WebSocket | SignalR |
|---------|--------------|---------|
| Protocol | Custom | SignalR Protocol |
| Reconnection | Manual | Automatic |
| Message Format | Custom JSON/Binary | MessagePack/JSON |
| Fallback | None | SSE, Long Polling |
| RPC | Manual | Built-in |
| Groups | Manual | Built-in |
| Performance | Higher | Lower overhead |
| Complexity | Higher | Lower |

## Future Enhancements

1. **Authentication**: JWT token validation
2. **Rooms/Groups**: Message routing to groups
3. **Presence**: Online user tracking
4. **File Transfer**: Chunked binary transfers
5. **Compression**: Per-message deflate
6. **Metrics**: Connection and message analytics