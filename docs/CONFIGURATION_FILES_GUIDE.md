# Configuration Files Guide - Collaborative Puzzle Platform

## Overview

This guide provides a comprehensive explanation of all YAML and JSON configuration files in the Collaborative Puzzle Platform. These files are essential for application behavior, deployment, and infrastructure management across different environments.

## Table of Contents

1. [Application Configuration Files](#application-configuration-files)
2. [Kubernetes YAML Files](#kubernetes-yaml-files)
3. [Docker Configuration](#docker-configuration)
4. [Build Configuration](#build-configuration)
5. [Development Tools Configuration](#development-tools-configuration)

## Application Configuration Files

### appsettings.json (Base Configuration)

**Location**: `src/CollaborativePuzzle.Api/appsettings.json`

**Purpose**: Default configuration for all environments

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CollaborativePuzzle;Trusted_Connection=true;TrustServerCertificate=true;",
    "Redis": "localhost:6379"
  }
}
```

**Key Settings Explained**:

- **Logging.LogLevel**: Controls verbosity of application logs
  - `Default`: General application logging level
  - `Microsoft.AspNetCore`: Framework-specific logging
  - Values: Trace, Debug, Information, Warning, Error, Critical, None

- **AllowedHosts**: Security feature preventing host header attacks
  - `"*"`: Allows all hosts (use specific domains in production)
  - Example: `"puzzle.example.com;*.puzzle.example.com"`

- **ConnectionStrings**: Database and cache connection information
  - `DefaultConnection`: SQL Server connection string
  - `Redis`: Redis cache connection string
  - `TrustServerCertificate=true`: Required for self-signed certificates

### appsettings.Development.json

**Location**: `src/CollaborativePuzzle.Api/appsettings.Development.json`

**Purpose**: Development-specific overrides

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.AspNetCore.Http.Connections": "Debug"
    }
  },
  "DetailedErrors": true,
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=CollaborativePuzzle;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;",
    "Redis": "localhost:6379"
  }
}
```

**Development-Specific Settings**:

- **Enhanced Logging**: Debug level for troubleshooting
- **DetailedErrors**: Shows full exception details (security risk in production)
- **Local Connections**: Points to Docker containers on localhost

### appsettings.Production.json

**Location**: `k8s/overlays/prod/appsettings.Production.json`

**Purpose**: Production environment configuration

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-app-insights-key"
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "KeepAliveInterval": 15,
    "ClientTimeoutInterval": 30,
    "MaximumReceiveMessageSize": 65536
  },
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": 60,
    "QueueLimit": 50
  }
}
```

**Production Settings Explained**:

- **ApplicationInsights**: Azure monitoring and telemetry
  - `InstrumentationKey`: Unique identifier for telemetry data

- **SignalR Configuration**:
  - `EnableDetailedErrors`: false for security
  - `KeepAliveInterval`: Ping interval in seconds
  - `ClientTimeoutInterval`: Disconnect timeout in seconds
  - `MaximumReceiveMessageSize`: 64KB limit prevents DoS

- **RateLimiting**: API throttling configuration
  - `PermitLimit`: 100 requests per window
  - `Window`: 60-second sliding window
  - `QueueLimit`: 50 queued requests maximum

### launchSettings.json

**Location**: `src/CollaborativePuzzle.Api/Properties/launchSettings.json`

**Purpose**: IDE launch profiles and development server configuration

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Profile Settings**:
- **commandName**: How to launch the application
- **launchBrowser**: Auto-open browser on start
- **applicationUrl**: Local development URLs
- **environmentVariables**: Runtime environment settings

## Kubernetes YAML Files

### namespace.yaml

**Purpose**: Creates isolated environment for application resources

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: puzzle-platform
  labels:
    name: puzzle-platform
    environment: production
```

**Why Namespaces Are Needed**:
- **Resource Isolation**: Separates application from system resources
- **Access Control**: RBAC policies can be namespace-scoped
- **Resource Quotas**: Limit CPU/memory per namespace
- **Network Policies**: Control traffic between namespaces

### configmap.yaml

**Purpose**: Non-sensitive configuration data storage

**Structure**:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: puzzle-config
data:
  appsettings.Production.json: |
    { configuration content }
  nginx.conf: |
    upstream puzzle-api { }
```

**Benefits**:
- **Environment Separation**: Different configs per environment
- **Dynamic Updates**: Change config without rebuilding images
- **Volume Mounting**: Files appear in container filesystem
- **Template Variables**: Reference in pod specifications

### secrets.yaml

**Purpose**: Secure storage for sensitive data

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: puzzle-secrets
type: Opaque
stringData:
  SQL_PASSWORD: "YourStrong@Passw0rd"
  REDIS_PASSWORD: "redis-secret"
  JWT_SECRET: "256-bit-secret-key"
```

**Security Features**:
- **Base64 Encoding**: Automatic encoding of stringData
- **RBAC Protection**: Access controlled by Kubernetes
- **Encryption at Rest**: When etcd encryption is enabled
- **Audit Logging**: Track secret access

### Deployment Files

#### api-deployment.yaml

**Purpose**: Defines how the API application runs

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: puzzle-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: puzzle-api
  template:
    spec:
      containers:
      - name: api
        image: puzzleplatform/api:latest
        ports:
        - containerPort: 80
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

**Key Specifications**:

- **Replicas**: Number of pod instances
  - Development: 1 replica
  - Production: 3-5 replicas for HA

- **Resource Management**:
  - `limits`: Maximum resources (prevents resource hogging)
  - `requests`: Guaranteed resources (for scheduling)
  - Memory: Mi (Mebibytes), Gi (Gibibytes)
  - CPU: m (millicores), 1000m = 1 CPU core

- **Health Probes**:
  ```yaml
  livenessProbe:
    httpGet:
      path: /health
      port: 80
    initialDelaySeconds: 30
    periodSeconds: 10
  ```
  - `livenessProbe`: Restarts unhealthy containers
  - `readinessProbe`: Removes from load balancer when not ready

#### redis-deployment.yaml

**Purpose**: Redis cache with high availability

```yaml
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: redis
        command:
        - redis-server
        - --requirepass
        - $(REDIS_PASSWORD)
        - --maxmemory
        - "256mb"
        - --maxmemory-policy
        - allkeys-lru
```

**Redis Configuration**:
- **Security**: Password from environment variable
- **Memory Management**: 
  - `maxmemory`: Prevents OOM
  - `allkeys-lru`: Eviction policy for cache
- **Clustering**: Multiple replicas for redundancy

#### sqlserver-deployment.yaml (StatefulSet)

**Purpose**: Persistent database with stable identity

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: puzzle-db
spec:
  serviceName: puzzle-db-service
  replicas: 1
  template:
    spec:
      containers:
      - name: sqlserver
        image: mcr.microsoft.com/mssql/server:2022-latest
  volumeClaimTemplates:
  - metadata:
      name: sql-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 8Gi
```

**StatefulSet Benefits**:
- **Stable Network Identity**: puzzle-db-0.puzzle-db-service
- **Ordered Deployment**: Sequential pod creation
- **Persistent Storage**: Survives pod recreation
- **Unique Storage**: Each replica gets own volume

### Service Files

**Purpose**: Network endpoints for pod access

```yaml
apiVersion: v1
kind: Service
metadata:
  name: puzzle-api-service
spec:
  selector:
    app: puzzle-api
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
```

**Service Types**:
- **ClusterIP**: Internal cluster access only (default)
- **NodePort**: Exposes on each node's IP
- **LoadBalancer**: Cloud provider load balancer
- **ExternalName**: Maps to external DNS

### ingress.yaml

**Purpose**: External HTTP(S) access to services

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  annotations:
    nginx.ingress.kubernetes.io/websocket-services: "puzzle-api-service"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
    nginx.ingress.kubernetes.io/affinity: "cookie"
    nginx.ingress.kubernetes.io/affinity-mode: "persistent"
spec:
  rules:
  - host: puzzle.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: puzzle-api-service
            port:
              number: 80
```

**Ingress Annotations Explained**:
- **websocket-services**: Enables WebSocket upgrade
- **proxy-timeouts**: Long timeouts for persistent connections
- **affinity**: Sticky sessions for SignalR
- **SSL/TLS**: Managed by cert-manager annotations

### hpa.yaml (Horizontal Pod Autoscaler)

**Purpose**: Automatic scaling based on metrics

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: puzzle-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
    scaleUp:
      stabilizationWindowSeconds: 60
```

**Scaling Behavior**:
- **Metrics**: CPU, memory, or custom metrics
- **Stabilization**: Prevents flapping
- **Scale Up**: Fast response to load (60s)
- **Scale Down**: Conservative reduction (300s)

### network-policies.yaml

**Purpose**: Microsegmentation security

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: api-network-policy
spec:
  podSelector:
    matchLabels:
      app: puzzle-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: puzzle-platform
    ports:
    - protocol: TCP
      port: 80
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: puzzle-redis
  - to:
    - podSelector:
        matchLabels:
          app: puzzle-db
```

**Security Benefits**:
- **Zero Trust**: Deny by default
- **Microsegmentation**: Pod-level isolation
- **Compliance**: Meet security requirements
- **Attack Surface**: Minimize lateral movement

### kustomization.yaml

**Purpose**: Configuration management without templates

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

resources:
- namespace.yaml
- configmap.yaml
- secrets.yaml
- redis-deployment.yaml
- sqlserver-deployment.yaml
- api-deployment.yaml
- services.yaml
- ingress.yaml

commonLabels:
  app.kubernetes.io/name: puzzle-platform
  app.kubernetes.io/version: v1.0.0

configMapGenerator:
- name: puzzle-config
  files:
  - appsettings.Production.json

secretGenerator:
- name: puzzle-secrets
  literals:
  - SQL_PASSWORD=YourStrong@Passw0rd
```

**Kustomize Features**:
- **Overlays**: Environment-specific changes
- **Patches**: Surgical modifications
- **Generators**: Dynamic resource creation
- **Transformers**: Cross-cutting changes

## Docker Configuration

### docker-compose.yml

**Purpose**: Local development environment orchestration

```yaml
version: '3.8'

services:
  api:
    build: 
      context: .
      dockerfile: src/CollaborativePuzzle.Api/Dockerfile
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - sqlserver
      - redis
    networks:
      - puzzle-network

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    networks:
      - puzzle-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - puzzle-network

volumes:
  sqlserver-data:
  redis-data:

networks:
  puzzle-network:
    driver: bridge
```

**Docker Compose Benefits**:
- **Service Dependencies**: Ordered startup
- **Networking**: Automatic DNS between containers
- **Volume Management**: Persistent data
- **Environment Isolation**: Separate from host

### Dockerfile

**Purpose**: Container image build instructions

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj", "src/CollaborativePuzzle.Api/"]
RUN dotnet restore "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj"
COPY . .
WORKDIR "/src/src/CollaborativePuzzle.Api"
RUN dotnet build "CollaborativePuzzle.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CollaborativePuzzle.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]
```

**Multi-stage Build Benefits**:
- **Smaller Images**: Final image only contains runtime
- **Build Caching**: Layers reused between builds
- **Security**: No build tools in production image

### .dockerignore

**Purpose**: Exclude files from Docker build context

```
**/bin/
**/obj/
**/.vs/
**/.git/
**/node_modules/
**/*.user
**/Dockerfile*
**/docker-compose*
**/.gitignore
**/.dockerignore
**/README.md
```

**Performance Impact**:
- Reduces build context size
- Faster builds
- Prevents accidental secret inclusion

## Build Configuration

### Directory.Build.props

**Purpose**: Centralized MSBuild properties

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All"/>
  </ItemGroup>
</Project>
```

**Build Configuration Benefits**:
- **Consistency**: Same settings across all projects
- **Maintainability**: Single location for versions
- **Code Quality**: Warnings as errors
- **Debugging**: SourceLink for production debugging

### global.json

**Purpose**: .NET SDK version pinning

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

**Version Control Benefits**:
- **Reproducible Builds**: Same SDK version
- **CI/CD Compatibility**: Matches build servers
- **Feature Stability**: Controls update behavior

## Development Tools Configuration

### .editorconfig

**Purpose**: Cross-IDE code style consistency

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{cs,vb}]
# C# Code Style Rules
dotnet_sort_system_directives_first = true
dotnet_style_qualification_for_field = false:warning
dotnet_style_prefer_auto_properties = true:warning

[*.{json,yml,yaml}]
indent_size = 2
```

**Code Style Benefits**:
- **Team Consistency**: Same formatting rules
- **Merge Conflicts**: Fewer whitespace issues
- **Code Reviews**: Focus on logic, not style

### omnisharp.json

**Purpose**: C# language server configuration

```json
{
  "FormattingOptions": {
    "EnableEditorConfigSupport": true,
    "OrganizeImports": true
  },
  "RoslynExtensionsOptions": {
    "EnableAnalyzersSupport": true,
    "EnableImportCompletion": true
  }
}
```

## Configuration Best Practices

### 1. Environment Variables
- Never hardcode secrets
- Use strong typing with IOptions<T>
- Validate configuration on startup

### 2. File Organization
- Separate configs by environment
- Use consistent naming conventions
- Document non-obvious settings

### 3. Security
- Encrypt secrets at rest
- Rotate credentials regularly
- Use least-privilege access

### 4. Performance
- Cache configuration values
- Minimize config file size
- Use appropriate data types

### 5. Maintainability
- Version control all configs
- Use descriptive keys
- Include inline comments

## Troubleshooting Configuration Issues

### Common Problems:

1. **Missing Configuration**
   - Check environment variables
   - Verify file mounting in containers
   - Review Kubernetes ConfigMaps

2. **Connection Failures**
   - Validate connection strings
   - Check network policies
   - Verify service discovery

3. **Permission Errors**
   - Review RBAC settings
   - Check file permissions
   - Validate secret access

4. **Performance Issues**
   - Review resource limits
   - Check timeout settings
   - Monitor connection pools

This comprehensive guide covers all configuration aspects of the Collaborative Puzzle Platform, explaining not just what each setting does, but why it's needed and how it impacts the application.