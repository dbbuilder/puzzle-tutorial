# Kubernetes Architecture - Collaborative Puzzle Platform

## System Architecture Diagram

```mermaid
graph TB
    subgraph "External Traffic"
        Users[Users/Browsers]
        IoT[IoT Devices]
        Mobile[Mobile Apps]
    end
    
    subgraph "Kubernetes Cluster"
        subgraph "Ingress Layer"
            Ingress[Nginx Ingress Controller]
            Cert[Cert Manager]
        end
        
        subgraph "puzzle-platform namespace"
            subgraph "Application Tier"
                API1[API Pod 1]
                API2[API Pod 2]
                API3[API Pod 3]
                APIS[API Service]
            end
            
            subgraph "Caching Tier"
                Redis1[Redis Pod 1]
                Redis2[Redis Pod 2]
                Redis3[Redis Pod 3]
                RedisS[Redis Service]
            end
            
            subgraph "Database Tier"
                DB[SQL Server Pod]
                DBS[DB Service]
                PVC1[DB PersistentVolume]
            end
            
            subgraph "Messaging Tier"
                MQTT1[MQTT Pod 1]
                MQTT2[MQTT Pod 2]
                MQTTS[MQTT Service]
            end
            
            subgraph "Monitoring"
                SM[ServiceMonitor]
                Metrics[Metrics Endpoint]
            end
        end
        
        subgraph "System Components"
            HPA[HorizontalPodAutoscaler]
            NP[NetworkPolicies]
            CM[ConfigMaps]
            Secrets[Secrets]
        end
    end
    
    subgraph "External Services"
        Prometheus[Prometheus]
        Grafana[Grafana]
        Storage[Cloud Storage]
    end
    
    Users --> Ingress
    IoT --> Ingress
    Mobile --> Ingress
    
    Ingress --> APIS
    APIS --> API1
    APIS --> API2
    APIS --> API3
    
    API1 --> RedisS
    API2 --> RedisS
    API3 --> RedisS
    
    RedisS --> Redis1
    RedisS --> Redis2
    RedisS --> Redis3
    
    API1 --> DBS
    API2 --> DBS
    API3 --> DBS
    DBS --> DB
    DB --> PVC1
    
    API1 --> MQTTS
    API2 --> MQTTS
    API3 --> MQTTS
    MQTTS --> MQTT1
    MQTTS --> MQTT2
    
    HPA --> APIS
    NP --> APIS
    NP --> RedisS
    NP --> DBS
    NP --> MQTTS
    
    CM --> API1
    CM --> API2
    CM --> API3
    
    Secrets --> API1
    Secrets --> API2
    Secrets --> API3
    Secrets --> DB
    Secrets --> Redis1
    
    SM --> Metrics
    Metrics --> Prometheus
    Prometheus --> Grafana
```

## Network Flow Diagram

```mermaid
graph LR
    subgraph "Client Connections"
        HTTP[HTTP/REST API]
        WS[WebSocket]
        SSE[Server-Sent Events]
        GRPC[gRPC]
    end
    
    subgraph "Ingress Rules"
        Rule1[puzzle.example.com/*]
        Rule2[ws.puzzle.example.com/puzzlehub]
        Rule3[ws.puzzle.example.com/webrtchub]
        Rule4[ws.puzzle.example.com/socket.io]
    end
    
    subgraph "Services"
        APIService[puzzle-api-service:80]
        RedisService[puzzle-redis-service:6379]
        DBService[puzzle-db-service:1433]
        MQTTService[puzzle-mqtt-service:1883]
    end
    
    subgraph "Network Policies"
        NPApi[API → Redis,DB,MQTT]
        NPRedis[Redis ← API only]
        NPDB[DB ← API only]
        NPMQTT[MQTT ← API only]
    end
    
    HTTP --> Rule1
    WS --> Rule2
    WS --> Rule3
    WS --> Rule4
    
    Rule1 --> APIService
    Rule2 --> APIService
    Rule3 --> APIService
    Rule4 --> APIService
    
    APIService --> NPApi
    NPApi --> RedisService
    NPApi --> DBService
    NPApi --> MQTTService
    
    RedisService --> NPRedis
    DBService --> NPDB
    MQTTService --> NPMQTT
```

## Pod Lifecycle and Scaling

```mermaid
sequenceDiagram
    participant User
    participant Ingress
    participant HPA
    participant Deployment
    participant Pod
    participant Service
    
    User->>Ingress: Increased Traffic
    Ingress->>Service: Route Requests
    Service->>Pod: Handle Requests
    Pod->>HPA: Report Metrics
    
    Note over HPA: CPU > 70%
    
    HPA->>Deployment: Scale Up
    Deployment->>Pod: Create New Pod
    Pod->>Pod: Init Container
    Pod->>Pod: Pull Image
    Pod->>Pod: Start Container
    Pod->>Service: Register Endpoint
    
    Note over Pod: Readiness Probe Success
    
    Service->>Pod: Route Traffic
    Pod->>User: Handle Requests
```

## Data Flow Architecture

```mermaid
graph TB
    subgraph "Real-time Data Flow"
        Client[Client Browser]
        SignalR[SignalR Hub]
        Redis[Redis Pub/Sub]
        API1[API Instance 1]
        API2[API Instance 2]
        API3[API Instance 3]
    end
    
    Client -->|WebSocket| SignalR
    SignalR -->|Publish| Redis
    Redis -->|Subscribe| API1
    Redis -->|Subscribe| API2
    Redis -->|Subscribe| API3
    API1 -->|Broadcast| Client
    API2 -->|Broadcast| Client
    API3 -->|Broadcast| Client
```

## Security Architecture

```mermaid
graph TB
    subgraph "Security Layers"
        subgraph "Network Security"
            NetworkPolicy[Network Policies]
            TLS[TLS Termination]
            Firewall[WAF/Firewall Rules]
        end
        
        subgraph "Application Security"
            Auth[JWT Authentication]
            RBAC[Role-Based Access]
            CORS[CORS Policies]
        end
        
        subgraph "Data Security"
            Secrets[K8s Secrets]
            Encryption[Encryption at Rest]
            Vault[External Vault]
        end
        
        subgraph "Runtime Security"
            PSP[Pod Security Policies]
            SA[Service Accounts]
            Admission[Admission Controllers]
        end
    end
    
    NetworkPolicy --> Auth
    TLS --> CORS
    Secrets --> Encryption
    PSP --> SA
```

## Storage Architecture

```mermaid
graph LR
    subgraph "Persistent Storage"
        subgraph "Database Storage"
            SQLPod[SQL Server Pod]
            SQLPVC[PVC: sql-data]
            SQLPV[PersistentVolume]
            Disk1[Azure Disk/AWS EBS]
        end
        
        subgraph "Cache Storage"
            RedisPod[Redis Pod]
            RedisPVC[PVC: redis-data]
            RedisPV[PersistentVolume]
            Disk2[SSD Storage]
        end
        
        subgraph "Message Storage"
            MQTTPod[MQTT Pod]
            MQTTPVC[PVC: mqtt-data]
            MQTTPV[PersistentVolume]
            Disk3[Standard Storage]
        end
    end
    
    SQLPod --> SQLPVC
    SQLPVC --> SQLPV
    SQLPV --> Disk1
    
    RedisPod --> RedisPVC
    RedisPVC --> RedisPV
    RedisPV --> Disk2
    
    MQTTPod --> MQTTPVC
    MQTTPVC --> MQTTPV
    MQTTPV --> Disk3
```

## Configuration Management

```mermaid
graph TB
    subgraph "Configuration Sources"
        Git[Git Repository]
        Kustomize[Kustomize]
        Helm[Helm Charts]
    end
    
    subgraph "Configuration Types"
        Base[Base Configuration]
        Dev[Dev Overlay]
        Staging[Staging Overlay]
        Prod[Prod Overlay]
    end
    
    subgraph "K8s Resources"
        CM[ConfigMaps]
        Secret[Secrets]
        Env[Environment Vars]
        Files[Mounted Files]
    end
    
    subgraph "Application"
        Pod[Application Pod]
        Container[Container]
        App[.NET Application]
    end
    
    Git --> Kustomize
    Kustomize --> Base
    Base --> Dev
    Base --> Staging
    Base --> Prod
    
    Dev --> CM
    Dev --> Secret
    
    CM --> Env
    CM --> Files
    Secret --> Env
    
    Env --> Container
    Files --> Container
    Container --> App
```

## High Availability Design

```mermaid
graph TB
    subgraph "Zone A"
        NodeA1[Node 1]
        NodeA2[Node 2]
        PodA1[API Pod]
        PodA2[Redis Pod]
        PodA3[MQTT Pod]
    end
    
    subgraph "Zone B"
        NodeB1[Node 3]
        NodeB2[Node 4]
        PodB1[API Pod]
        PodB2[Redis Pod]
        PodB3[DB Pod]
    end
    
    subgraph "Zone C"
        NodeC1[Node 5]
        NodeC2[Node 6]
        PodC1[API Pod]
        PodC2[Redis Pod]
        PodC3[MQTT Pod]
    end
    
    subgraph "Load Balancer"
        LB[Cloud Load Balancer]
    end
    
    LB --> PodA1
    LB --> PodB1
    LB --> PodC1
    
    PodA1 -.-> PodA2
    PodA1 -.-> PodB3
    PodA1 -.-> PodA3
    
    PodB1 -.-> PodB2
    PodB1 -.-> PodB3
    PodB1 -.-> PodC3
    
    PodC1 -.-> PodC2
    PodC1 -.-> PodB3
    PodC1 -.-> PodC3
```

## Deployment Pipeline

```mermaid
graph LR
    subgraph "CI/CD Pipeline"
        Code[Source Code]
        Build[Build Stage]
        Test[Test Stage]
        Image[Container Image]
        Registry[Container Registry]
        Deploy[Deploy Stage]
    end
    
    subgraph "Environments"
        Dev[Development]
        Staging[Staging]
        Prod[Production]
    end
    
    Code --> Build
    Build --> Test
    Test --> Image
    Image --> Registry
    Registry --> Deploy
    
    Deploy --> Dev
    Dev -->|Promote| Staging
    Staging -->|Promote| Prod
```

This architecture demonstrates:
- **High Availability**: Multiple replicas across availability zones
- **Scalability**: Horizontal pod autoscaling based on metrics
- **Security**: Multiple layers of network and application security
- **Observability**: Integrated monitoring and metrics collection
- **Resilience**: Health checks, circuit breakers, and graceful shutdowns
- **Performance**: Caching layer, connection pooling, and load balancing