# Collaborative Puzzle Platform - Architecture Overview

## System Architecture Diagram

```mermaid
graph TB
    subgraph "Client Layer"
        WEB[Web Browser]
        MOB[Mobile App]
        IOT[IoT Device]
    end
    
    subgraph "API Gateway"
        GW[Azure Application Gateway]
        LB[Load Balancer]
    end
    
    subgraph "Application Layer"
        API1[API Server 1]
        API2[API Server 2]
        API3[API Server N]
    end
    
    subgraph "Real-time Communication"
        SIG[SignalR Hub]
        WS[WebSocket Handler]
        MQTT[MQTT Broker]
        TURN[TURN Server]
    end
    
    subgraph "Cache Layer"
        REDIS1[Redis Master]
        REDIS2[Redis Replica]
        REDISPUB[Redis Pub/Sub]
    end
    
    subgraph "Data Layer"
        SQL[SQL Server]
        BLOB[Blob Storage]
    end
    
    WEB --> GW
    MOB --> GW
    IOT --> MQTT
    
    GW --> LB
    LB --> API1
    LB --> API2
    LB --> API3
    
    API1 --> SIG
    API2 --> SIG
    API3 --> SIG
    
    SIG --> REDISPUB
    WS --> REDIS1
    MQTT --> REDIS1
    
    API1 --> SQL
    API2 --> SQL
    API3 --> SQL
    
    API1 --> BLOB
    
    REDIS1 --> REDIS2
    
    WEB -.->|WebRTC| TURN
    MOB -.->|WebRTC| TURN
```

## Technology Interconnections

### 1. SignalR + Redis Backplane

SignalR uses Redis as a backplane to synchronize messages across multiple servers:

```mermaid
sequenceDiagram
    participant C1 as Client 1
    participant S1 as Server 1
    participant R as Redis Pub/Sub
    participant S2 as Server 2
    participant C2 as Client 2
    
    C1->>S1: MovePiece(pieceId, x, y)
    S1->>S1: Validate & Update DB
    S1->>R: Publish to puzzle:{sessionId}
    R->>S2: Relay message
    S2->>C2: PieceMoved notification
    S1->>C1: Confirmation
```

**Code Reference**: [`src/CollaborativePuzzle.Hubs/PuzzleHub.cs`](../src/CollaborativePuzzle.Hubs/PuzzleHub.cs#L232-L242)

### 2. Distributed Locking Pattern

Redis provides distributed locks to prevent race conditions:

```mermaid
sequenceDiagram
    participant U1 as User 1
    participant U2 as User 2
    participant R as Redis
    participant DB as Database
    
    U1->>R: SET piece-lock:123 user1 NX EX 30
    R-->>U1: OK (lock acquired)
    U2->>R: SET piece-lock:123 user2 NX EX 30
    R-->>U2: NULL (lock denied)
    U1->>DB: Update piece position
    U1->>R: DEL piece-lock:123
    Note over U2: Can now acquire lock
```

**Code Reference**: [`src/CollaborativePuzzle.Hubs/PuzzleHub.cs`](../src/CollaborativePuzzle.Hubs/PuzzleHub.cs#L285-L289)

### 3. WebSocket vs SignalR

```mermaid
graph LR
    subgraph "SignalR Stack"
        SR[SignalR Hub]
        MP[MessagePack]
        TP[Transport Layer]
    end
    
    subgraph "Raw WebSocket"
        WS[WebSocket Handler]
        BIN[Binary Protocol]
    end
    
    subgraph "Shared Infrastructure"
        REDIS[Redis Cache]
        SQL[SQL Server]
    end
    
    SR --> MP
    MP --> TP
    TP --> REDIS
    
    WS --> BIN
    BIN --> REDIS
    
    SR --> SQL
    WS --> SQL
```

### 4. WebRTC Signaling Flow

```mermaid
sequenceDiagram
    participant A as Alice
    participant S as SignalR Hub
    participant R as Redis
    participant B as Bob
    participant T as TURN Server
    
    A->>S: CreateOffer()
    S->>R: Store offer
    S->>B: OfferReceived
    B->>S: CreateAnswer()
    S->>R: Store answer
    S->>A: AnswerReceived
    
    Note over A,B: ICE Candidate Exchange
    A->>S: ICE Candidate
    S->>B: ICE Candidate
    B->>S: ICE Candidate
    S->>A: ICE Candidate
    
    A<-->T: STUN/TURN
    B<-->T: STUN/TURN
    A<-->B: P2P Connection Established
```

### 5. MQTT Integration

```mermaid
graph TB
    subgraph "MQTT Broker"
        BROKER[MQTT Server]
        TOPIC1[puzzle/+/moves]
        TOPIC2[puzzle/+/chat]
        TOPIC3[puzzle/+/status]
    end
    
    subgraph "Bridge Layer"
        BRIDGE[MQTT-SignalR Bridge]
        TRANS[Protocol Translator]
    end
    
    subgraph "Clients"
        IOT1[Puzzle Display]
        IOT2[Smart Table]
        WEB[Web Client]
    end
    
    IOT1 -->|Publish| TOPIC1
    IOT2 -->|Subscribe| TOPIC1
    
    BROKER --> BRIDGE
    BRIDGE --> TRANS
    TRANS --> WEB
    
    WEB -->|SignalR| TRANS
    TRANS -->|MQTT| BROKER
```

### 6. HTTP/3 QUIC Stack

```mermaid
graph TB
    subgraph "Traditional HTTP/2"
        H2[HTTP/2]
        TLS2[TLS 1.3]
        TCP[TCP]
    end
    
    subgraph "HTTP/3 Stack"
        H3[HTTP/3]
        QUIC[QUIC]
        UDP[UDP]
    end
    
    subgraph "Benefits"
        FAST[0-RTT Connection]
        MUX[Stream Multiplexing]
        MOBILE[Better Mobile Performance]
    end
    
    H3 --> FAST
    QUIC --> MUX
    UDP --> MOBILE
```

## Data Flow Patterns

### Real-time Piece Movement

```mermaid
flowchart LR
    A[User Drags Piece] --> B{Piece Locked?}
    B -->|No| C[Acquire Lock]
    B -->|Yes| D[Show Error]
    C --> E[Update Position]
    E --> F[Broadcast via SignalR]
    F --> G[Update Redis Cache]
    F --> H[Update SQL DB]
    G --> I[Notify Other Servers]
    I --> J[Update Other Clients]
```

### Session State Management

```mermaid
stateDiagram-v2
    [*] --> Created: CreateSession
    Created --> Active: FirstUserJoins
    Active --> InProgress: PuzzleStarted
    InProgress --> Paused: AllUsersLeave
    Paused --> InProgress: UserRejoins
    InProgress --> Completed: AllPiecesPlaced
    Completed --> [*]
    
    note right of Active: Stored in Redis\nwith 8hr TTL
    note right of InProgress: Tracked in SQL\n+ Redis cache
```

## Scalability Patterns

### Horizontal Scaling

```mermaid
graph TB
    subgraph "Single Server"
        S1[Server]
        M1[In-Memory State]
    end
    
    subgraph "Multi-Server with Redis"
        S2[Server 1]
        S3[Server 2]
        S4[Server N]
        R[Redis Backplane]
        
        S2 -.-> R
        S3 -.-> R
        S4 -.-> R
    end
    
    subgraph "Kubernetes Deployment"
        POD1[Pod 1]
        POD2[Pod 2]
        POD3[Pod N]
        SVC[Service]
        CM[ConfigMap]
        SEC[Secrets]
        
        SVC --> POD1
        SVC --> POD2
        SVC --> POD3
        
        CM --> POD1
        SEC --> POD1
    end
```

### Caching Strategy

```mermaid
graph LR
    subgraph "Cache Layers"
        L1[Browser Cache]
        L2[CDN Cache]
        L3[Redis Cache]
        L4[SQL Cache]
    end
    
    subgraph "Cache Keys"
        K1[puzzle:{id}]
        K2[session:{id}]
        K3[user:{id}:session]
        K4[piece-lock:{id}]
    end
    
    REQ[Request] --> L1
    L1 -->|Miss| L2
    L2 -->|Miss| L3
    L3 -->|Miss| L4
    L4 -->|Miss| DB[Database]
    
    L3 --> K1
    L3 --> K2
    L3 --> K3
    L3 --> K4
```

## Security Architecture

```mermaid
graph TB
    subgraph "Security Layers"
        AUTH[Authentication - Azure AD]
        AUTHZ[Authorization - Policies]
        VAL[Input Validation]
        RATE[Rate Limiting]
        ENC[Encryption]
    end
    
    subgraph "Protected Resources"
        API[API Endpoints]
        HUB[SignalR Hubs]
        WS[WebSocket Endpoints]
        MQTT[MQTT Topics]
    end
    
    CLIENT[Client] --> AUTH
    AUTH --> AUTHZ
    AUTHZ --> VAL
    VAL --> RATE
    RATE --> API
    RATE --> HUB
    RATE --> WS
    
    API --> ENC
    HUB --> ENC
    WS --> ENC
    
    ENC --> DB[Database]
```

## Monitoring & Observability

```mermaid
graph TB
    subgraph "Metrics Collection"
        APP[Application]
        PROM[Prometheus Metrics]
        AI[Application Insights]
        CUSTOM[Custom Telemetry]
    end
    
    subgraph "Dashboards"
        GRAF[Grafana]
        AZURE[Azure Monitor]
        KIBANA[Kibana]
    end
    
    subgraph "Alerts"
        ALERT1[High Error Rate]
        ALERT2[Slow Response Time]
        ALERT3[Connection Spike]
        ALERT4[Memory Pressure]
    end
    
    APP --> PROM
    APP --> AI
    APP --> CUSTOM
    
    PROM --> GRAF
    AI --> AZURE
    CUSTOM --> KIBANA
    
    GRAF --> ALERT1
    AZURE --> ALERT2
    AZURE --> ALERT3
    KIBANA --> ALERT4
```

## Technology Decision Matrix

| Feature | SignalR | WebSocket | WebRTC | MQTT | Socket.IO |
|---------|---------|-----------|---------|------|-----------|
| Use Case | General real-time | Low-level control | P2P communication | IoT devices | Legacy support |
| Scaling | Excellent (Redis) | Manual | Complex | Good | Good |
| Browser Support | Excellent | Good | Good | Via bridge | Excellent |
| Protocol | Multiple | WS only | UDP/TCP | TCP | Multiple |
| Complexity | Low | Medium | High | Low | Low |
| Our Usage | Primary | Learning | Voice chat | IoT demo | Compatibility |

## References

- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr)
- [Redis Pub/Sub](https://redis.io/docs/manual/pubsub/)
- [WebRTC Specification](https://www.w3.org/TR/webrtc/)
- [MQTT Protocol](https://mqtt.org/mqtt-specification/)
- [HTTP/3 Explained](https://http3-explained.haxx.se/)