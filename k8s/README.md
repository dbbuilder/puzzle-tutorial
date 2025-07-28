# Kubernetes Deployment for Collaborative Puzzle Platform

This directory contains Kubernetes manifests for deploying the Collaborative Puzzle Platform using Kustomize.

## Directory Structure

```
k8s/
├── base/                    # Base configuration
│   ├── deployment.yaml      # Main API deployment
│   ├── service.yaml         # Service definitions
│   ├── configmap.yaml       # Configuration
│   ├── secret.yaml          # Secret template
│   ├── ingress.yaml         # Ingress configuration
│   ├── hpa.yaml            # Horizontal Pod Autoscaler
│   ├── redis.yaml          # Redis deployment
│   ├── postgres.yaml       # PostgreSQL StatefulSet
│   ├── mqtt.yaml           # MQTT broker deployment
│   └── kustomization.yaml  # Kustomize base
└── overlays/
    ├── dev/                # Development environment
    ├── staging/            # Staging environment
    └── production/         # Production environment
```

## Prerequisites

- Kubernetes cluster (1.21+)
- kubectl installed and configured
- Kustomize (built into kubectl 1.14+)
- NGINX Ingress Controller
- cert-manager (for TLS certificates)

## Quick Start

### Development Deployment

```bash
# Create namespace
kubectl create namespace puzzle-platform-dev

# Deploy using Kustomize
kubectl apply -k k8s/overlays/dev

# Check deployment status
kubectl -n puzzle-platform-dev get pods
kubectl -n puzzle-platform-dev get svc
kubectl -n puzzle-platform-dev get ingress
```

### Production Deployment

```bash
# Create namespace
kubectl create namespace puzzle-platform-prod

# Create production secrets (example)
kubectl -n puzzle-platform-prod create secret generic puzzle-secrets \
  --from-literal=redis-connection-string="redis-master:6379,password=yourpassword" \
  --from-literal=database-connection-string="Server=postgres;Database=CollaborativePuzzle;..."

# Deploy using Kustomize
kubectl apply -k k8s/overlays/production

# Check deployment
kubectl -n puzzle-platform-prod get pods
```

## Architecture

### Core Components

1. **puzzle-api**: Main API service with SignalR, WebSocket, and REST endpoints
   - 3 replicas in production
   - Horizontal autoscaling enabled
   - Session affinity for SignalR

2. **redis-master**: Redis instance for:
   - SignalR backplane
   - Distributed caching
   - Rate limiting state

3. **postgres**: PostgreSQL database
   - StatefulSet with persistent storage
   - 20GB volume in production

4. **mqtt-broker**: Eclipse Mosquitto for MQTT protocol
   - WebSocket support on port 9001
   - Standard MQTT on port 1883

### Networking

- **Ingress**: NGINX with WebSocket support
- **Services**: 
  - ClusterIP for internal communication
  - Headless service for SignalR sticky sessions
- **Session Affinity**: Cookie-based for SignalR connections

### Storage

- **Redis**: 10GB PersistentVolume
- **PostgreSQL**: 20GB PersistentVolume with StatefulSet
- **ConfigMaps**: Application settings
- **Secrets**: Connection strings and credentials

## Configuration

### Environment Variables

Key environment variables configured:
- `ASPNETCORE_ENVIRONMENT`: Development/Staging/Production
- `ConnectionStrings__Redis`: Redis connection string
- `ConnectionStrings__DefaultConnection`: Database connection string

### Health Checks

All deployments include:
- **Liveness Probe**: `/health/live`
- **Readiness Probe**: `/health/ready`
- **Startup Probe**: `/health/startup`

### Resource Limits

Development:
- API: 128Mi-256Mi memory, 100m-250m CPU
- Redis: 256Mi-512Mi memory, 100m-250m CPU
- PostgreSQL: 256Mi-512Mi memory, 250m-500m CPU

Production:
- API: 512Mi-1Gi memory, 500m-1000m CPU
- Autoscaling: 3-20 replicas based on CPU/memory

## Security

1. **Network Policies**: Restrict traffic between pods (not included, cluster-specific)
2. **Pod Security**:
   - Non-root user (UID 1000)
   - Read-only root filesystem
   - No privilege escalation
   - Drop all capabilities

3. **Secrets Management**:
   - Use sealed-secrets or external-secrets-operator in production
   - Never commit actual secrets to Git
   - Rotate credentials regularly

## Monitoring

Recommended monitoring stack:
- Prometheus for metrics collection
- Grafana for visualization
- Application exposes metrics on port 8081

## Troubleshooting

### Check Pod Logs
```bash
kubectl -n puzzle-platform-dev logs -f deployment/puzzle-api
kubectl -n puzzle-platform-dev logs -f deployment/redis-master
```

### Debug WebSocket Issues
```bash
# Check ingress configuration
kubectl -n puzzle-platform-dev describe ingress puzzle-api-ingress

# Test WebSocket connection
curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" \
  -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==" \
  https://puzzle-api-dev.example.com/puzzlehub
```

### Common Issues

1. **SignalR Connection Failures**
   - Check session affinity configuration
   - Verify WebSocket headers in ingress
   - Ensure Redis is accessible

2. **Database Connection Issues**
   - Verify PostgreSQL pod is running
   - Check connection string in secrets
   - Ensure network connectivity

3. **High Memory Usage**
   - Check for memory leaks
   - Adjust resource limits
   - Enable memory dumps for debugging

## Production Checklist

- [ ] Update image tags to specific versions (not `latest`)
- [ ] Configure proper domain names in ingress
- [ ] Set up TLS certificates with cert-manager
- [ ] Configure external secrets management
- [ ] Set up monitoring and alerting
- [ ] Configure backup for PostgreSQL
- [ ] Test disaster recovery procedures
- [ ] Review and apply network policies
- [ ] Enable pod disruption budgets
- [ ] Configure cluster autoscaling

## Customization

To customize for your environment:

1. Copy an overlay directory
2. Modify kustomization.yaml
3. Add patches as needed
4. Apply with `kubectl apply -k k8s/overlays/your-env`

Example customizations:
- Different replica counts
- Custom domains
- Environment-specific secrets
- Resource adjustments
- Additional labels/annotations