# WebRTC, STUN, and TURN Implementation Guide

## Overview
WebRTC (Web Real-Time Communication) enables peer-to-peer audio, video, and data communication directly between browsers. STUN and TURN servers help establish these connections through NAT and firewall obstacles.

## Purpose in Our Application
- **Voice chat** between puzzle collaborators
- **Screen sharing** for collaborative problem-solving
- **Peer-to-peer data channels** for reduced server load
- **Direct file sharing** of custom puzzle images
- **Real-time video calls** for enhanced collaboration

## Architecture Components

### 1. WebRTC Components
```
Puzzle Client A <-> STUN/TURN Servers <-> Puzzle Client B
        |                                        |
        +-- SignalR Hub (Signaling Server) ------+
```

### 2. Signaling Server (SignalR Hub)
**File**: `src/CollaborativePuzzle.Hubs/WebRTCSignalingHub.cs`

```csharp
public class WebRTCSignalingHub : Hub
{
    // Exchange ICE candidates between peers
    // Coordinate offer/answer exchange
    // Manage voice chat room membership
}
```

### 3. STUN Server Configuration
STUN (Session Traversal Utilities for NAT) helps discover public IP addresses and port mappings.

**Configuration in appsettings.json**:
```json
{
  "WebRTC": {
    "IceServers": [
      {
        "urls": "stun:stun.l.google.com:19302"
      },
      {
        "urls": "stun:stun1.l.google.com:19302"
      }
    ]
  }
}
```

### 4. TURN Server Setup
TURN (Traversal Using Relays around NAT) relays traffic when direct connection fails.

**Azure Communication Services Integration**:
```json
{
  "WebRTC": {
    "IceServers": [
      {
        "urls": "turn:turnserver.azure.com:3478",
        "username": "generated-username",
        "credential": "generated-password"
      }
    ]
  }
}
```

## Implementation Details

### 1. Voice Chat Setup
**File**: `src/CollaborativePuzzle.Frontend/src/services/VoiceChatService.ts`

```typescript
export class VoiceChatService {
    private peerConnection: RTCPeerConnection;
    private localStream: MediaStream;
    private signalRConnection: HubConnection;
    
    async initializeVoiceChat(sessionId: string) {
        // Configure ICE servers
        this.peerConnection = new RTCPeerConnection({
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { 
                    urls: 'turn:your-turn-server.com:3478',
                    username: 'username',
                    credential: 'password'
                }
            ]
        });
        
        // Get user media (microphone)
        this.localStream = await navigator.mediaDevices.getUserMedia({
            audio: true,
            video: false
        });
        
        // Add local stream to peer connection
        this.localStream.getTracks().forEach(track => {
            this.peerConnection.addTrack(track, this.localStream);
        });
    }
}
```

### 2. Signaling Protocol
The signaling process coordinates the WebRTC connection establishment:

```typescript
// 1. User A creates offer
const offer = await peerConnection.createOffer();
await peerConnection.setLocalDescription(offer);
signalRConnection.invoke('SendOffer', sessionId, offer);

// 2. User B receives offer and creates answer
signalRConnection.on('ReceiveOffer', async (offer) => {
    await peerConnection.setRemoteDescription(offer);
    const answer = await peerConnection.createAnswer();
    await peerConnection.setLocalDescription(answer);
    signalRConnection.invoke('SendAnswer', sessionId, answer);
});

// 3. ICE candidate exchange
peerConnection.onicecandidate = (event) => {
    if (event.candidate) {
        signalRConnection.invoke('SendIceCandidate', sessionId, event.candidate);
    }
};
```

### 3. NAT Traversal Handling
Different NAT types require different strategies:

```typescript
class NATTraversalManager {
    async attemptConnection(): Promise<RTCPeerConnection> {
        const configuration: RTCConfiguration = {
            iceServers: [
                // STUN servers for NAT discovery
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                
                // TURN servers for relay (fallback)
                {
                    urls: 'turn:your-turn-server.com:3478',
                    username: await this.getTurnUsername(),
                    credential: await this.getTurnCredential()
                }
            ],
            iceCandidatePoolSize: 10
        };
        
        return new RTCPeerConnection(configuration);
    }
}
```

## Audio Processing Features

### 1. Audio Quality Management
```typescript
class AudioQualityManager {
    setupAudioProcessing(stream: MediaStream) {
        const audioContext = new AudioContext();
        const source = audioContext.createMediaStreamSource(stream);
        
        // Noise suppression
        const noiseSuppressionNode = audioContext.createScriptProcessor(4096, 1, 1);
        
        // Echo cancellation (browser handles this automatically)
        
        // Automatic gain control
        const gainNode = audioContext.createGain();
        gainNode.gain.value = 1.0;
        
        source.connect(noiseSuppressionNode);
        noiseSuppressionNode.connect(gainNode);
        
        return stream;
    }
}
```

### 2. Mute/Unmute Controls
```typescript
class VoiceControls {
    private localStream: MediaStream;
    
    muteAudio(): void {
        this.localStream.getAudioTracks().forEach(track => {
            track.enabled = false;
        });
    }
    
    unmuteAudio(): void {
        this.localStream.getAudioTracks().forEach(track => {
            track.enabled = true;
        });
    }
    
    adjustVolume(volume: number): void {
        // Adjust gain for local audio
        // Volume is between 0.0 and 1.0
    }
}
```

## Screen Sharing Implementation

### 1. Screen Capture
```typescript
class ScreenSharingService {
    async startScreenShare(): Promise<MediaStream> {
        try {
            const screenStream = await navigator.mediaDevices.getDisplayMedia({
                video: {
                    cursor: 'always',
                    frameRate: 30,
                    width: { ideal: 1920, max: 1920 },
                    height: { ideal: 1080, max: 1080 }
                },
                audio: true
            });
            
            return screenStream;
        } catch (error) {
            throw new Error('Screen sharing not supported or permission denied');
        }
    }
    
    async shareScreen(peerConnection: RTCPeerConnection): Promise<void> {
        const screenStream = await this.startScreenShare();
        
        // Replace video track in existing connection
        const sender = peerConnection.getSenders().find(s => 
            s.track && s.track.kind === 'video'
        );
        
        if (sender) {
            await sender.replaceTrack(screenStream.getVideoTracks()[0]);
        } else {
            peerConnection.addTrack(screenStream.getVideoTracks()[0], screenStream);
        }
    }
}
```

## Data Channels for P2P Communication

### 1. Data Channel Setup
```typescript
class P2PDataService {
    private dataChannel: RTCDataChannel;
    
    createDataChannel(peerConnection: RTCPeerConnection): void {
        this.dataChannel = peerConnection.createDataChannel('puzzleData', {
            ordered: true,
            maxRetransmits: 3
        });
        
        this.dataChannel.onopen = () => {
            console.log('Data channel opened');
        };
        
        this.dataChannel.onmessage = (event) => {
            this.handleDataMessage(JSON.parse(event.data));
        };
    }
    
    sendPieceUpdate(pieceId: string, x: number, y: number): void {
        if (this.dataChannel.readyState === 'open') {
            const message = {
                type: 'pieceUpdate',
                pieceId,
                x,
                y,
                timestamp: Date.now()
            };
            
            this.dataChannel.send(JSON.stringify(message));
        }
    }
}
```

## Error Handling and Fallbacks

### 1. Connection State Management
```typescript
class ConnectionStateManager {
    monitorConnection(peerConnection: RTCPeerConnection): void {
        peerConnection.onconnectionstatechange = () => {
            switch (peerConnection.connectionState) {
                case 'connected':
                    this.handleConnectionEstablished();
                    break;
                case 'disconnected':
                    this.handleConnectionLost();
                    break;
                case 'failed':
                    this.handleConnectionFailed();
                    break;
                case 'closed':
                    this.handleConnectionClosed();
                    break;
            }
        };
    }
    
    async handleConnectionFailed(): Promise<void> {
        // Attempt reconnection with fresh TURN credentials
        // Fall back to server-relayed communication if WebRTC fails
        await this.fallbackToSignalR();
    }
}
```

### 2. TURN Server Failover
```typescript
class TurnServerManager {
    private turnServers: TurnServer[];
    private currentServerIndex: number = 0;
    
    async getNextTurnServer(): Promise<RTCIceServer> {
        if (this.currentServerIndex >= this.turnServers.length) {
            throw new Error('No more TURN servers available');
        }
        
        const server = this.turnServers[this.currentServerIndex++];
        const credentials = await this.refreshTurnCredentials(server);
        
        return {
            urls: server.urls,
            username: credentials.username,
            credential: credentials.credential
        };
    }
}
```

## Security Considerations

### 1. TURN Credential Management
```csharp
// Server-side TURN credential generation
public class TurnCredentialService
{
    public async Task<TurnCredentials> GenerateCredentialsAsync(string userId)
    {
        var username = $"{userId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600}";
        var password = ComputeHmacSha1(username, _turnSecret);
        
        return new TurnCredentials
        {
            Username = username,
            Credential = password,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
    }
}
```

### 2. Media Stream Permissions
```typescript
class MediaPermissionManager {
    async requestAudioPermission(): Promise<boolean> {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            stream.getTracks().forEach(track => track.stop()); // Clean up test stream
            return true;
        } catch (error) {
            console.error('Audio permission denied:', error);
            return false;
        }
    }
    
    async requestScreenSharePermission(): Promise<boolean> {
        try {
            const stream = await navigator.mediaDevices.getDisplayMedia({ video: true });
            stream.getTracks().forEach(track => track.stop()); // Clean up test stream
            return true;
        } catch (error) {
            console.error('Screen share permission denied:', error);
            return false;
        }
    }
}
```

## Performance Optimization

### 1. Codec Selection
```typescript
class CodecOptimizer {
    optimizeAudioCodecs(peerConnection: RTCPeerConnection): void {
        const transceivers = peerConnection.getTransceivers();
        
        transceivers.forEach(transceiver => {
            if (transceiver.sender.track?.kind === 'audio') {
                const capabilities = RTCRtpSender.getCapabilities('audio');
                const preferredCodecs = capabilities?.codecs.filter(codec => 
                    codec.mimeType === 'audio/opus'
                ) || [];
                
                transceiver.setCodecPreferences(preferredCodecs);
            }
        });
    }
}
```

### 2. Bandwidth Management
```typescript
class BandwidthManager {
    async adjustQuality(peerConnection: RTCPeerConnection, targetBitrate: number): Promise<void> {
        const senders = peerConnection.getSenders();
        
        for (const sender of senders) {
            if (sender.track?.kind === 'audio') {
                const params = sender.getParameters();
                params.encodings[0].maxBitrate = targetBitrate;
                await sender.setParameters(params);
            }
        }
    }
}
```

## Testing and Debugging

### 1. WebRTC Statistics
```typescript
class WebRTCDebugger {
    async collectStats(peerConnection: RTCPeerConnection): Promise<RTCStatsReport> {
        const stats = await peerConnection.getStats();
        
        stats.forEach((report) => {
            if (report.type === 'inbound-rtp' && report.kind === 'audio') {
                console.log('Audio quality:', {
                    packetsLost: report.packetsLost,
                    jitter: report.jitter,
                    bytesReceived: report.bytesReceived
                });
            }
        });
        
        return stats;
    }
}
```

## Integration with Puzzle Platform

### 1. Session Integration
```typescript
// Integrate voice chat with puzzle sessions
class PuzzleVoiceIntegration {
    async joinVoiceChat(sessionId: string): Promise<void> {
        // Join voice room when joining puzzle session
        await this.voiceChatService.joinRoom(sessionId);
        
        // Show voice indicators on user cursors
        this.puzzleRenderer.enableVoiceIndicators();
    }
    
    async enablePushToTalk(): Promise<void> {
        // Implement push-to-talk for focused puzzle solving
        document.addEventListener('keydown', (event) => {
            if (event.code === 'Space') {
                this.voiceChatService.unmute();
            }
        });
        
        document.addEventListener('keyup', (event) => {
            if (event.code === 'Space') {
                this.voiceChatService.mute();
            }
        });
    }
}
```

### 2. UI Integration
```vue
<template>
  <div class="puzzle-container">
    <!-- Puzzle canvas -->
    <PuzzleCanvas />
    
    <!-- Voice chat controls -->
    <VoiceChatControls 
      :is-muted="isMuted"
      :participants="voiceParticipants"
      @toggle-mute="toggleMute"
      @start-screen-share="startScreenShare"
    />
  </div>
</template>
```

This comprehensive WebRTC implementation provides high-quality voice communication and screen sharing capabilities that enhance the collaborative puzzle-solving experience while maintaining security and performance standards.
