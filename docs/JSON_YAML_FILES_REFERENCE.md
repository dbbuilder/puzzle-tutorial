# JSON and YAML Files Reference - Quick Lookup Guide

## JSON Files in the Project

### Application Settings Files

| File | Location | Purpose | Key Settings |
|------|----------|---------|--------------|
| `appsettings.json` | `/src/CollaborativePuzzle.Api/` | Base configuration for all environments | Connection strings, logging defaults |
| `appsettings.Development.json` | `/src/CollaborativePuzzle.Api/` | Development overrides | Debug logging, local connections |
| `appsettings.Production.json` | `/k8s/base/` and `/k8s/overlays/prod/` | Production settings | Rate limiting, SignalR config |
| `launchSettings.json` | `/src/CollaborativePuzzle.Api/Properties/` | IDE launch profiles | URLs, environment variables |
| `global.json` | `/` (root) | .NET SDK version lock | SDK version, roll-forward policy |
| `omnisharp.json` | `/` (root) | C# language server config | Code formatting, analysis |

### Package Configuration Files

| File | Location | Purpose | Key Settings |
|------|----------|---------|--------------|
| `package.json` | Various test directories | NPM package definitions | Dependencies, scripts |
| `package-lock.json` | Various test directories | Dependency lock file | Exact versions |
| `tsconfig.json` | TypeScript projects | TypeScript compiler config | Compilation options |

## YAML Files in the Project

### Kubernetes Base Manifests (`/k8s/base/`)

| File | Resource Type | Purpose | Critical Configuration |
|------|---------------|---------|------------------------|
| `namespace.yaml` | Namespace | Creates isolated environment | Name: puzzle-platform |
| `configmap.yaml` | ConfigMap | Non-sensitive configs | appsettings, nginx.conf |
| `secrets.yaml` | Secret | Sensitive data | Passwords, JWT keys |
| `redis-deployment.yaml` | Deployment | Redis cache pods | 3 replicas, password auth |
| `sqlserver-deployment.yaml` | StatefulSet | SQL Server database | Persistent storage, 8Gi |
| `mqtt-deployment.yaml` | Deployment | MQTT broker | Ports 1883, 9001 |
| `api-deployment.yaml` | Deployment | Main API application | 3 replicas, health checks |
| `services.yaml` | Service | Network endpoints | ClusterIP services |
| `ingress.yaml` | Ingress | External access | WebSocket support, TLS |
| `hpa.yaml` | HorizontalPodAutoscaler | Auto-scaling | CPU 70%, 3-10 replicas |
| `network-policies.yaml` | NetworkPolicy | Network security | Pod-to-pod rules |
| `service-monitor.yaml` | ServiceMonitor | Prometheus integration | Metrics collection |
| `kustomization.yaml` | Kustomization | Build configuration | Resource list, patches |

### Kubernetes Overlays (`/k8s/overlays/`)

| File | Environment | Purpose | Key Differences |
|------|-------------|---------|-----------------|
| `dev/kustomization.yaml` | Development | Dev customization | Single replicas, debug mode |
| `dev/deployment-patch.yaml` | Development | Resource reduction | Lower CPU/memory |
| `prod/kustomization.yaml` | Production | Prod customization | High replicas, monitoring |
| `prod/deployment-patch.yaml` | Production | Resource increase | Higher CPU/memory |
| `prod/ingress-patch.yaml` | Production | Domain config | Real SSL certs |
| `prod/resource-patch.yaml` | Production | Fine-tuning | Optimized resources |

### Docker Configuration

| File | Location | Purpose | Key Configuration |
|------|----------|---------|-------------------|
| `docker-compose.yml` | `/` (root) | Local development stack | Services, networks, volumes |
| `docker-compose.override.yml` | `/` (root) | Local overrides | Development-specific settings |

### CI/CD Files

| File | Location | Purpose | Key Features |
|------|----------|---------|--------------|
| `.github/workflows/*.yml` | `/.github/workflows/` | GitHub Actions | Build, test, deploy |
| `azure-pipelines.yml` | `/` (root) | Azure DevOps | CI/CD pipeline |

## Quick Reference - File Purposes

### Configuration Hierarchy
```
appsettings.json (base)
  └── appsettings.Development.json (dev overrides)
  └── appsettings.Production.json (prod overrides)
      └── ConfigMap in Kubernetes (mounted as file)
          └── Environment variables (highest priority)
```

### Deployment Flow
```
docker-compose.yml (local dev)
  └── Dockerfile (build image)
      └── Kubernetes manifests (deployment)
          └── Kustomize overlays (environment-specific)
              └── Deployed application
```

### Network Configuration Chain
```
services.yaml (internal endpoints)
  └── ingress.yaml (external access)
      └── network-policies.yaml (security rules)
          └── nginx.conf in ConfigMap (routing rules)
```

## File Relationships

### API Deployment Dependencies
- `api-deployment.yaml` → references:
  - `configmap.yaml` (configuration)
  - `secrets.yaml` (credentials)
  - `services.yaml` (networking)
  - `hpa.yaml` (autoscaling)

### Database Stack
- `sqlserver-deployment.yaml` → creates:
  - StatefulSet (stable identity)
  - PersistentVolumeClaim (storage)
  - Service (network endpoint)

### Redis Cache Stack  
- `redis-deployment.yaml` → creates:
  - Deployment (replicated pods)
  - Service (load balancing)
  - NetworkPolicy (security)

## Common Configuration Patterns

### Environment Variables in Pods
```yaml
env:
- name: ASPNETCORE_ENVIRONMENT
  value: "Production"
- name: SQL_PASSWORD
  valueFrom:
    secretKeyRef:
      name: puzzle-secrets
      key: SQL_PASSWORD
```

### Volume Mounts for Configuration
```yaml
volumeMounts:
- name: config
  mountPath: /app/appsettings.Production.json
  subPath: appsettings.Production.json
- name: secrets
  mountPath: /app/secrets
  readOnly: true
```

### Resource Specifications
```yaml
resources:
  limits:    # Maximum allowed
    memory: "512Mi"
    cpu: "1000m"
  requests:  # Guaranteed minimum
    memory: "256Mi"
    cpu: "200m"
```

### Service Selectors
```yaml
selector:
  app: puzzle-api      # Must match pod labels
ports:
- port: 80            # Service port
  targetPort: 80      # Container port
  name: http          # Port name for reference
```

## Troubleshooting Guide

| Issue | Check These Files | Common Problems |
|-------|-------------------|-----------------|
| App won't start | appsettings.*.json, configmap.yaml | Missing connection strings |
| Can't connect to DB | secrets.yaml, services.yaml | Wrong password or service name |
| No external access | ingress.yaml, services.yaml | Missing ingress rules |
| Pods keep restarting | deployment yamls, hpa.yaml | Resource limits too low |
| Network timeouts | network-policies.yaml | Blocked by network policy |
| Config not loading | configmap.yaml, deployment yaml | Volume mount misconfigured |

## Security Checklist

- [ ] Secrets using Secret resources, not ConfigMaps
- [ ] Network policies restricting pod communication  
- [ ] Resource limits preventing DoS
- [ ] Non-root containers in Dockerfiles
- [ ] TLS enabled in ingress
- [ ] Passwords rotated regularly
- [ ] RBAC configured (not shown in files)

This reference guide provides quick access to understanding the purpose and relationships of all JSON and YAML configuration files in the project.