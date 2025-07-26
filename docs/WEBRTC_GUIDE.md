# WebRTC Implementation Guide

## Overview

The Collaborative Puzzle Platform includes a complete WebRTC implementation for real-time video, audio, and screen sharing capabilities. This guide covers the architecture, usage, and best practices.

## Architecture

### Components

1. **WebRTCHub** - SignalR hub for signaling
2. **STUN/TURN Servers** - NAT traversal  
3. **Peer Connections** - Direct P2P communication
4. **Media Streams** - Audio/video handling

### Signaling Flow

```mermaid
sequenceDiagram
    participant A as User A
    participant H as WebRTC Hub
    participant B as User B
    
    A->>H: Connect to Hub
    B->>H: Connect to Hub
    
    A->>H: Join Room
    B->>H: Join Room
    
    A->>H: Request Call
    H->>B: Incoming Call
    B->>H: Accept Call
    H->>A: Call Accepted
    
    A->>H: Send Offer
    H->>B: Receive Offer
    B->>H: Send Answer
    H->>A: Receive Answer
    
    A->>H: Send ICE Candidate
    H->>B: Receive ICE Candidate
    B->>H: Send ICE Candidate
    H->>A: Receive ICE Candidate
    
    A<-->B: P2P Connection Established
```

## Getting Started

### 1. Connect to WebRTC Hub

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/webrtchub')
    .withAutomaticReconnect()
    .build();

await connection.start();
```

### 2. Join a Room

```javascript
const result = await connection.invoke('JoinRoom', 'room-123');
if (result.success) {
    console.log('Joined room:', result.roomId);
    console.log('ICE servers:', result.iceServers);
}
```

### 3. Handle Incoming Calls

```javascript
connection.on('IncomingCall', async (data) => {
    const accepted = confirm(`${data.fromUserId} is calling. Accept?`);
    
    await connection.invoke('RespondToCall', data.from, {
        accepted: accepted,
        reason: accepted ? null : 'User declined'
    });
});
```

### 4. Create Peer Connection

```javascript
const pc = new RTCPeerConnection({
    iceServers: result.iceServers
});

// Add local stream
const stream = await navigator.mediaDevices.getUserMedia({
    video: true,
    audio: true
});

stream.getTracks().forEach(track => {
    pc.addTrack(track, stream);
});

// Handle remote stream
pc.ontrack = (event) => {
    remoteVideo.srcObject = event.streams[0];
};
```

## Hub Methods

### Connection Management

#### JoinRoom
```csharp
Task<RoomJoinResult> JoinRoom(string roomId)
```
Join a room for WebRTC communication.

**Returns:**
```json
{
    "success": true,
    "roomId": "room-123",
    "participants": ["user1", "user2"],
    "iceServers": [
        {
            "urls": ["stun:stun.l.google.com:19302"],
        },
        {
            "urls": ["turn:turnserver.com:3478"],
            "username": "user",
            "credential": "pass"
        }
    ]
}
```

#### LeaveRoom
```csharp
Task LeaveRoom(string roomId)
```
Leave the current room.

### Call Management

#### RequestCall
```csharp
Task RequestCall(string targetConnectionId, CallRequest request)
```
Initiate a call with another user.

**Request:**
```json
{
    "callType": "video", // "audio" or "video"
    "metadata": {
        "customData": "value"
    }
}
```

#### RespondToCall
```csharp
Task RespondToCall(string targetConnectionId, CallResponse response)
```
Respond to an incoming call request.

**Response:**
```json
{
    "accepted": true,
    "reason": null // or rejection reason
}
```

#### EndCall
```csharp
Task EndCall(string targetConnectionId)
```
End an active call.

### WebRTC Signaling

#### SendOffer
```csharp
Task SendOffer(string targetConnectionId, RTCSessionDescription offer)
```
Send SDP offer to establish connection.

#### SendAnswer
```csharp
Task SendAnswer(string targetConnectionId, RTCSessionDescription answer)
```
Send SDP answer in response to offer.

#### SendIceCandidate
```csharp
Task SendIceCandidate(string targetConnectionId, RTCIceCandidate candidate)
```
Send ICE candidate for NAT traversal.

### Utility Methods

#### GetOnlineUsers
```csharp
Task<List<OnlineUser>> GetOnlineUsers()
```
Get list of currently online users.

## Client Events

### Connection Events

- **Connected** - Connection established with hub
- **UserJoined** - User joined the room
- **UserLeft** - User left the room

### Call Events

- **IncomingCall** - Incoming call request
- **CallResponse** - Response to call request
- **CallEnded** - Call terminated

### WebRTC Events

- **ReceiveOffer** - SDP offer received
- **ReceiveAnswer** - SDP answer received
- **ReceiveIceCandidate** - ICE candidate received

## Media Constraints

### Recommended Settings

```javascript
const mediaConstraints = {
    video: {
        width: { ideal: 1280, max: 1920 },
        height: { ideal: 720, max: 1080 },
        frameRate: { ideal: 30, max: 60 },
        facingMode: 'user' // or 'environment'
    },
    audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
        sampleRate: 48000
    }
};
```

### Screen Sharing

```javascript
const screenStream = await navigator.mediaDevices.getDisplayMedia({
    video: {
        cursor: 'always',
        displaySurface: 'monitor'
    },
    audio: false // or true for system audio
});

// Replace video track
const videoTrack = screenStream.getVideoTracks()[0];
const sender = pc.getSenders().find(s => s.track?.kind === 'video');
await sender.replaceTrack(videoTrack);

// Handle screen share end
videoTrack.onended = () => {
    // Revert to camera
    sender.replaceTrack(cameraTrack);
};
```

## STUN/TURN Configuration

### Development (docker-compose.yml)

```yaml
coturn:
  image: coturn/coturn:latest
  ports:
    - "3478:3478/udp"
    - "3478:3478/tcp"
  environment:
    - TURNSERVER_ENABLED=1
    - TURN_USER=puzzle
    - TURN_PASSWORD=puzzle123
    - TURN_REALM=puzzle.local
```

### Production Configuration

```json
{
  "iceServers": [
    {
      "urls": [
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
      ]
    },
    {
      "urls": "turn:your-turn-server.com:3478",
      "username": "production-user",
      "credential": "secure-password"
    },
    {
      "urls": "turns:your-turn-server.com:5349",
      "username": "production-user",
      "credential": "secure-password"
    }
  ]
}
```

## Best Practices

### 1. Connection Management

```javascript
// Always clean up on disconnect
window.addEventListener('beforeunload', async () => {
    if (pc) pc.close();
    if (connection) await connection.stop();
});

// Handle connection failures
pc.onconnectionstatechange = () => {
    if (pc.connectionState === 'failed') {
        // Attempt reconnection
        recreatePeerConnection();
    }
};
```

### 2. Media Handling

```javascript
// Request permissions early
async function initializeMedia() {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: true
        });
        
        // Test and immediately stop to prime permissions
        stream.getTracks().forEach(track => track.stop());
    } catch (error) {
        console.error('Media permissions denied');
    }
}

// Handle device changes
navigator.mediaDevices.ondevicechange = async () => {
    // Update available devices
    await updateDeviceList();
};
```

### 3. Network Optimization

```javascript
// Bandwidth management
const transceiver = pc.addTransceiver('video', {
    direction: 'sendrecv',
    streams: [localStream]
});

const params = transceiver.sender.getParameters();
params.encodings[0].maxBitrate = 1000000; // 1 Mbps
await transceiver.sender.setParameters(params);

// Adaptive bitrate
pc.oniceconnectionstatechange = () => {
    if (pc.iceConnectionState === 'connected') {
        monitorConnectionQuality();
    }
};
```

### 4. Error Handling

```javascript
// Comprehensive error handling
async function handleCall() {
    try {
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        await connection.invoke('SendOffer', targetId, offer);
    } catch (error) {
        if (error.name === 'NotAllowedError') {
            showError('Camera/microphone access denied');
        } else if (error.name === 'NotFoundError') {
            showError('No camera/microphone found');
        } else if (error.name === 'OverconstrainedError') {
            showError('Camera constraints cannot be satisfied');
        } else {
            showError(`Call failed: ${error.message}`);
        }
    }
}
```

## Statistics Monitoring

```javascript
async function getConnectionStats() {
    const stats = await pc.getStats();
    const report = {};
    
    stats.forEach(stat => {
        if (stat.type === 'inbound-rtp') {
            report[stat.mediaType] = {
                bytesReceived: stat.bytesReceived,
                packetsReceived: stat.packetsReceived,
                packetsLost: stat.packetsLost,
                jitter: stat.jitter,
                frameRate: stat.framesPerSecond
            };
        }
    });
    
    return report;
}

// Monitor every second
setInterval(async () => {
    const stats = await getConnectionStats();
    updateStatsDisplay(stats);
}, 1000);
```

## Security Considerations

1. **HTTPS Required** - WebRTC requires secure context
2. **TURN Authentication** - Use time-limited credentials
3. **Signaling Security** - Authenticate SignalR connections
4. **Media Encryption** - DTLS-SRTP is mandatory
5. **Permission Handling** - Request minimal permissions

## Troubleshooting

### Common Issues

1. **No Video/Audio**
   - Check browser permissions
   - Verify device availability
   - Test with `getUserMedia` directly

2. **Connection Fails**
   - Verify STUN/TURN accessibility
   - Check firewall settings
   - Enable verbose logging

3. **Poor Quality**
   - Monitor network conditions
   - Adjust bitrate constraints
   - Check CPU usage

### Debug Mode

```javascript
// Enable detailed logging
pc.addEventListener('icecandidateerror', (event) => {
    console.error('ICE Candidate Error:', event);
});

// Log all state changes
['connectionstatechange', 'iceconnectionstatechange', 'signalingstatechange']
    .forEach(event => {
        pc.addEventListener(event, () => {
            console.log(`${event}:`, pc[event.replace('change', '')]);
        });
    });
```

## Testing

Use the provided test page at `/webrtc-test.html` to:

- Test peer connections
- Monitor connection quality
- Debug signaling flow
- Verify STUN/TURN servers
- Test different media constraints

## Performance Optimization

1. **Simulcast** - Send multiple video encodings
2. **SVC** - Scalable Video Coding for adaptability  
3. **FEC** - Forward Error Correction for packet loss
4. **Jitter Buffer** - Smooth playback management
5. **Echo Cancellation** - Acoustic echo suppression

## Future Enhancements

1. **Recording** - Client/server-side recording
2. **Transcription** - Real-time speech-to-text
3. **Virtual Backgrounds** - Canvas/ML processing
4. **Multi-party** - SFU/MCU integration
5. **AR Effects** - Face detection and filters