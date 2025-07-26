# Kubernetes Deployment Guide - Collaborative Puzzle Platform

## Overview

This guide explains the Kubernetes manifests and configuration files used to deploy the Collaborative Puzzle Platform. The deployment demonstrates modern cloud-native practices including microservices architecture, real-time communication protocols, container orchestration, and networking configurations.

## Directory Structure

```
k8s/
├── base/                      # Base configurations used across all environments
│   ├── namespace.yaml        # Kubernetes namespace definition
│   ├── configmap.yaml        # Application and nginx configurations
│   ├── secrets.yaml          # Sensitive credentials (encrypted in production)
│   ├── redis-deployment.yaml # Redis cache deployment
│   ├── sqlserver-deployment.yaml # SQL Server database
│   ├── mqtt-deployment.yaml  # MQTT broker for IoT messaging
│   ├── api-deployment.yaml   # Main API application
│   ├── ingress.yaml          # External access configuration
│   ├── hpa.yaml              # Horizontal Pod Autoscaler
│   ├── network-policies.yaml # Network security rules
│   ├── service-monitor.yaml  # Prometheus monitoring
│   └── kustomization.yaml    # Kustomize configuration
├── overlays/                  # Environment-specific configurations
│   ├── dev/                  # Development environment
│   └── prod/                 # Production environment
└── deploy.sh                 # Deployment automation script
```

## File Explanations

### 1. namespace.yaml
**Purpose**: Creates an isolated environment for the application

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: puzzle-platform
```

**Why needed**:
- **Isolation**: Separates resources from other applications
- **Resource Management**: Enables namespace-level quotas and limits
- **Security**: Allows namespace-scoped RBAC policies
- **Multi-tenancy**: Supports multiple environments (dev, staging, prod)

### 2. configmap.yaml
**Purpose**: Stores non-sensitive configuration data

**Key sections**:

#### Application Settings (appsettings.Production.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=puzzle-db-service;...",
    "Redis": "puzzle-redis-service:6379"
  }
}
```
- **Database Connection**: Points to internal Kubernetes service names
- **Redis Connection**: Uses cluster-internal DNS for service discovery
- **CORS Settings**: Configures allowed origins for cross-origin requests
- **SignalR Configuration**: 
  - `KeepAliveInterval`: Prevents idle connection timeouts
  - `ClientTimeoutInterval`: Maximum time before disconnecting inactive clients
  - `MaximumReceiveMessageSize`: Limits message size to prevent DoS attacks

#### Nginx Configuration
```nginx
upstream puzzle-api {
    server puzzle-api-service:80;
}
```
- **Load Balancing**: Distributes traffic across API pods
- **WebSocket Support**: Special handling for upgrade headers
- **Connection Persistence**: Maintains sticky sessions for SignalR

### 3. secrets.yaml
**Purpose**: Stores sensitive credentials securely

```yaml
stringData:
  SQL_PASSWORD: "YourStrong@Passw0rd"
  REDIS_PASSWORD: "redis-secret-password"
  TURN_SECRET: "turn-shared-secret"
  JWT_SECRET: "your-256-bit-secret-key"
```

**Security considerations**:
- **Base64 Encoding**: Kubernetes automatically encodes stringData
- **RBAC Protection**: Access controlled by Kubernetes RBAC
- **Environment Injection**: Secrets mounted as environment variables
- **Rotation Support**: Can be updated without pod restarts

### 4. redis-deployment.yaml
**Purpose**: Deploys Redis for caching and SignalR backplane

**Key configurations**:
```yaml
command:
- redis-server
- --requirepass
- $(REDIS_PASSWORD)
- --maxmemory
- "256mb"
- --maxmemory-policy
- allkeys-lru
```

**Why these settings**:
- **Password Protection**: Secures Redis access
- **Memory Limit**: Prevents memory exhaustion
- **LRU Eviction**: Automatically removes least-used keys when full
- **Health Probes**: Ensures Redis is responsive before routing traffic

### 5. sqlserver-deployment.yaml
**Purpose**: Deploys SQL Server as a StatefulSet for data persistence

**Key features**:
```yaml
kind: StatefulSet
serviceName: puzzle-db-service
volumeClaimTemplates:
- metadata:
    name: sql-data
  spec:
    accessModes: ["ReadWriteOnce"]
    resources:
      requests:
        storage: 8Gi
```

**Why StatefulSet**:
- **Stable Network Identity**: Consistent hostname for database connections
- **Ordered Deployment**: Ensures proper initialization
- **Persistent Storage**: Data survives pod restarts
- **Headless Service**: Direct pod addressing for database clustering

### 6. mqtt-deployment.yaml
**Purpose**: Deploys MQTT broker for IoT-style messaging

**Configuration highlights**:
```yaml
ports:
- containerPort: 1883  # Standard MQTT
- containerPort: 9001  # WebSocket MQTT
```

**Use cases**:
- **IoT Device Simulation**: Connects virtual sensors
- **Real-time Telemetry**: Streams sensor data
- **Pub/Sub Messaging**: Decoupled event distribution

### 7. api-deployment.yaml
**Purpose**: Main application deployment with high availability

**Critical settings**:
```yaml
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: api
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        resources:
          limits:
            memory: "512Mi"
            cpu: "1000m"
          requests:
            memory: "256Mi"
            cpu: "200m"
```

**Design decisions**:
- **Multiple Replicas**: High availability and load distribution
- **Resource Limits**: Prevents resource starvation
- **Resource Requests**: Ensures scheduling on appropriate nodes
- **Environment Variables**: Runtime configuration without rebuilds
- **Liveness/Readiness Probes**: Automatic failure recovery

### 8. ingress.yaml
**Purpose**: Exposes services to external traffic

**Key annotations**:
```yaml
annotations:
  nginx.ingress.kubernetes.io/websocket-services: "puzzle-api-service"
  nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
  nginx.ingress.kubernetes.io/affinity: "cookie"
```

**Why these matter**:
- **WebSocket Support**: Enables real-time connections
- **Extended Timeouts**: Supports long-lived connections
- **Session Affinity**: Routes clients to same backend pod
- **SSL/TLS**: Automatic certificate management with cert-manager

### 9. hpa.yaml (Horizontal Pod Autoscaler)
**Purpose**: Automatically scales pods based on load

**Scaling metrics**:
```yaml
metrics:
- type: Resource
  resource:
    name: cpu
    target:
      averageUtilization: 70
- type: Pods
  pods:
    metric:
      name: signalr_connections_per_pod
    target:
      averageValue: "1000"
```

**Scaling strategy**:
- **CPU-based**: Scales when CPU usage exceeds 70%
- **Custom Metrics**: Scales based on SignalR connections
- **Gradual Scaling**: Prevents flapping with stabilization windows
- **Min/Max Bounds**: Between 3 and 10 replicas

### 10. network-policies.yaml
**Purpose**: Implements zero-trust network security

**Security rules**:
```yaml
spec:
  podSelector:
    matchLabels:
      app: puzzle-api
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: puzzle-api
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: puzzle-redis
```

**Security benefits**:
- **Microsegmentation**: Only allows necessary connections
- **Defense in Depth**: Network-level security beyond application auth
- **Compliance**: Meets security audit requirements
- **Blast Radius Reduction**: Limits impact of compromised pods

### 11. service-monitor.yaml
**Purpose**: Integrates with Prometheus for monitoring

**Monitoring setup**:
```yaml
spec:
  endpoints:
  - port: metrics
    interval: 30s
    path: /metrics
```

**Alerts configured**:
- **High SignalR Connections**: Warns when connections exceed 5000
- **High Memory Usage**: Critical alert at 90% memory
- **High Error Rate**: Warns when 5xx errors exceed 5%

## Environment Overlays

### Development (k8s/overlays/dev/)
**Purpose**: Lightweight configuration for development

**Key differences**:
- Single replica for all services
- Reduced resource limits
- Debug logging enabled
- Self-signed certificates
- Local hostname (dev.puzzle.local)

### Production (k8s/overlays/prod/)
**Purpose**: High-performance, secure configuration

**Production features**:
- Multiple replicas (5 API, 3 Redis, 2 MQTT)
- Increased resource allocations
- Production logging levels
- Real SSL certificates
- Custom domain configuration
- Application Insights integration

## Kustomization Files

**Purpose**: Manages configuration variations without duplication

**Key features**:
```yaml
bases:
- ../../base

patchesStrategicMerge:
- deployment-patch.yaml

replicas:
- name: puzzle-api
  count: 5
```

**Benefits**:
- **DRY Principle**: Single source of truth for base configs
- **Environment Flexibility**: Easy to add new environments
- **Patch Management**: Surgical updates to specific fields
- **Build-time Resolution**: No runtime overhead

## Deployment Script (deploy.sh)

**Purpose**: Automates deployment process

**Key steps**:
1. Environment detection
2. Namespace creation
3. Kustomize build and apply
4. Health checking
5. Status reporting

**Usage**:
```bash
./deploy.sh dev   # Deploy to development
./deploy.sh prod  # Deploy to production
```

## Networking Demonstration

The deployment showcases multiple networking concepts:

1. **Service Discovery**: Internal DNS resolution (puzzle-api-service)
2. **Load Balancing**: Distributes traffic across pod replicas
3. **WebSocket Routing**: Maintains persistent connections
4. **Network Policies**: Implements microsegmentation
5. **Ingress Control**: External traffic management
6. **Session Affinity**: Sticky sessions for SignalR

## Security Layers

1. **Network Policies**: Pod-to-pod communication rules
2. **Secrets Management**: Encrypted credential storage
3. **RBAC**: Role-based access control (not shown, but assumed)
4. **TLS/SSL**: Encrypted external traffic
5. **Resource Limits**: Prevents DoS through resource exhaustion
6. **Health Checks**: Automatic removal of unhealthy pods

## Best Practices Demonstrated

1. **12-Factor App**: Environment-specific configuration
2. **Microservices**: Separate deployments for each service
3. **Immutable Infrastructure**: ConfigMaps and Secrets for configuration
4. **Observability**: Prometheus metrics and alerts
5. **High Availability**: Multiple replicas and health checks
6. **Security First**: Network policies and secrets management
7. **GitOps Ready**: Declarative configuration in version control

## Troubleshooting

Common issues and solutions:

1. **Pod CrashLoopBackOff**: Check logs with `kubectl logs`
2. **Service Unavailable**: Verify network policies
3. **Slow Performance**: Check HPA metrics and resource limits
4. **Connection Issues**: Verify ingress configuration
5. **Database Connection**: Ensure secrets are properly mounted

This Kubernetes deployment provides a production-ready example of deploying a real-time collaborative application with modern cloud-native practices.