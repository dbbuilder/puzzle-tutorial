# Production Deployment Guide

## Overview
This guide provides step-by-step instructions for deploying the Collaborative Puzzle Platform to production using Azure Kubernetes Service (AKS) and related Azure services.

## Prerequisites

### Required Tools
```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install kubectl
az aks install-cli

# Install Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install .NET SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0
```

### Azure Resources Required
- Azure Subscription
- Resource Group
- Azure Container Registry (ACR)
- Azure Kubernetes Service (AKS)
- Azure SQL Database
- Azure Cache for Redis
- Azure Storage Account
- Azure Key Vault
- Application Insights

## Step 1: Infrastructure Setup

### Create Resource Group
```bash
# Set variables
RESOURCE_GROUP="rg-puzzle-platform-prod"
LOCATION="eastus"
ENVIRONMENT="production"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### Create Azure Container Registry
```bash
ACR_NAME="acrpuzzleplatform"

# Create ACR
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Premium \
  --location $LOCATION

# Enable admin access
az acr update --name $ACR_NAME --admin-enabled true

# Get credentials
az acr credential show --name $ACR_NAME
```

### Create AKS Cluster
```bash
AKS_CLUSTER="aks-puzzle-platform"
NODE_COUNT=3
NODE_VM_SIZE="Standard_D4s_v3"

# Create AKS cluster
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count $NODE_COUNT \
  --node-vm-size $NODE_VM_SIZE \
  --network-plugin azure \
  --enable-managed-identity \
  --enable-addons monitoring,http_application_routing \
  --generate-ssh-keys \
  --attach-acr $ACR_NAME

# Get credentials
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER
```

### Create Azure SQL Database
```bash
SQL_SERVER="sql-puzzle-platform"
SQL_DATABASE="PuzzlePlatform"
SQL_ADMIN="sqladmin"
SQL_PASSWORD=$(openssl rand -base64 32)

# Create SQL Server
az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER \
  --location $LOCATION \
  --admin-user $SQL_ADMIN \
  --admin-password $SQL_PASSWORD

# Create database
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DATABASE \
  --edition "Standard" \
  --compute-model "Provisioned" \
  --family "Gen5" \
  --capacity 10

# Configure firewall
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Create Redis Cache
```bash
REDIS_NAME="redis-puzzle-platform"

# Create Redis
az redis create \
  --resource-group $RESOURCE_GROUP \
  --name $REDIS_NAME \
  --location $LOCATION \
  --sku Premium \
  --vm-size P1 \
  --enable-non-ssl-port false

# Get connection string
REDIS_KEY=$(az redis list-keys \
  --resource-group $RESOURCE_GROUP \
  --name $REDIS_NAME \
  --query primaryKey -o tsv)

REDIS_CONNECTION="${REDIS_NAME}.redis.cache.windows.net:6380,password=${REDIS_KEY},ssl=True,abortConnect=False"
```

### Create Storage Account
```bash
STORAGE_ACCOUNT="stpuzzleplatform"

# Create storage account
az storage account create \
  --resource-group $RESOURCE_GROUP \
  --name $STORAGE_ACCOUNT \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Get connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --resource-group $RESOURCE_GROUP \
  --name $STORAGE_ACCOUNT \
  --query connectionString -o tsv)

# Create blob container
az storage container create \
  --name "puzzles" \
  --connection-string $STORAGE_CONNECTION \
  --public-access off
```

### Create Key Vault
```bash
KEYVAULT_NAME="kv-puzzle-platform"

# Create Key Vault
az keyvault create \
  --resource-group $RESOURCE_GROUP \
  --name $KEYVAULT_NAME \
  --location $LOCATION \
  --sku Standard

# Add secrets
az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "SqlConnectionString" \
  --value "Server=tcp:${SQL_SERVER}.database.windows.net,1433;Database=${SQL_DATABASE};User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};Encrypt=true;TrustServerCertificate=false;"

az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "RedisConnectionString" \
  --value $REDIS_CONNECTION

az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "StorageConnectionString" \
  --value $STORAGE_CONNECTION
```

## Step 2: Application Configuration

### Create Kubernetes Namespace
```bash
kubectl create namespace puzzle-platform
kubectl config set-context --current --namespace=puzzle-platform
```

### Create ConfigMap
```yaml
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: puzzle-config
  namespace: puzzle-platform
data:
  appsettings.Production.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "ApplicationInsights": {
        "ConnectionString": "InstrumentationKey=YOUR_KEY"
      },
      "AzureKeyVault": {
        "VaultUri": "https://kv-puzzle-platform.vault.azure.net/"
      },
      "SignalR": {
        "RedisConnectionString": "@Microsoft.KeyVault(SecretUri=https://kv-puzzle-platform.vault.azure.net/secrets/RedisConnectionString)"
      },
      "ConnectionStrings": {
        "DefaultConnection": "@Microsoft.KeyVault(SecretUri=https://kv-puzzle-platform.vault.azure.net/secrets/SqlConnectionString)",
        "Redis": "@Microsoft.KeyVault(SecretUri=https://kv-puzzle-platform.vault.azure.net/secrets/RedisConnectionString)",
        "Storage": "@Microsoft.KeyVault(SecretUri=https://kv-puzzle-platform.vault.azure.net/secrets/StorageConnectionString)"
      }
    }
```

```bash
kubectl apply -f k8s/configmap.yaml
```

### Create Secrets
```bash
# Create Docker registry secret
kubectl create secret docker-registry acr-secret \
  --docker-server=${ACR_NAME}.azurecr.io \
  --docker-username=$ACR_NAME \
  --docker-password=$(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv) \
  --namespace=puzzle-platform
```

## Step 3: Build and Push Docker Images

### Backend API Dockerfile
```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj", "src/CollaborativePuzzle.Api/"]
COPY ["src/CollaborativePuzzle.Core/CollaborativePuzzle.Core.csproj", "src/CollaborativePuzzle.Core/"]
COPY ["src/CollaborativePuzzle.Infrastructure/CollaborativePuzzle.Infrastructure.csproj", "src/CollaborativePuzzle.Infrastructure/"]
RUN dotnet restore "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj"
COPY . .
WORKDIR "/src/src/CollaborativePuzzle.Api"
RUN dotnet build "CollaborativePuzzle.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CollaborativePuzzle.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]
```

### Frontend Dockerfile
```dockerfile
# Dockerfile.frontend
FROM node:18-alpine AS build
WORKDIR /app
COPY src/CollaborativePuzzle.Frontend/package*.json ./
RUN npm ci
COPY src/CollaborativePuzzle.Frontend/ .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Build and Push Images
```bash
# Login to ACR
az acr login --name $ACR_NAME

# Build and push API image
docker build -t ${ACR_NAME}.azurecr.io/puzzle-api:latest .
docker push ${ACR_NAME}.azurecr.io/puzzle-api:latest

# Build and push frontend image
docker build -f Dockerfile.frontend -t ${ACR_NAME}.azurecr.io/puzzle-frontend:latest .
docker push ${ACR_NAME}.azurecr.io/puzzle-frontend:latest
```

## Step 4: Kubernetes Deployment

### API Deployment
```yaml
# k8s/api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: puzzle-api
  namespace: puzzle-platform
spec:
  replicas: 3
  selector:
    matchLabels:
      app: puzzle-api
  template:
    metadata:
      labels:
        app: puzzle-api
        aadpodidbinding: puzzle-platform-identity
    spec:
      containers:
      - name: api
        image: acrpuzzleplatform.azurecr.io/puzzle-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:80"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.Production.json
          subPath: appsettings.Production.json
      volumes:
      - name: config
        configMap:
          name: puzzle-config
      imagePullSecrets:
      - name: acr-secret
---
apiVersion: v1
kind: Service
metadata:
  name: puzzle-api
  namespace: puzzle-platform
spec:
  selector:
    app: puzzle-api
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
```

### Frontend Deployment
```yaml
# k8s/frontend-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: puzzle-frontend
  namespace: puzzle-platform
spec:
  replicas: 2
  selector:
    matchLabels:
      app: puzzle-frontend
  template:
    metadata:
      labels:
        app: puzzle-frontend
    spec:
      containers:
      - name: frontend
        image: acrpuzzleplatform.azurecr.io/puzzle-frontend:latest
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
      imagePullSecrets:
      - name: acr-secret
---
apiVersion: v1
kind: Service
metadata:
  name: puzzle-frontend
  namespace: puzzle-platform
spec:
  selector:
    app: puzzle-frontend
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
```

### Ingress Configuration
```yaml
# k8s/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: puzzle-ingress
  namespace: puzzle-platform
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/websocket-services: "puzzle-api"
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"
spec:
  tls:
  - hosts:
    - puzzle.yourdomain.com
    secretName: puzzle-tls
  rules:
  - host: puzzle.yourdomain.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: puzzle-api
            port:
              number: 80
      - path: /puzzlehub
        pathType: Prefix
        backend:
          service:
            name: puzzle-api
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: puzzle-frontend
            port:
              number: 80
```

### Deploy to Kubernetes
```bash
# Apply all configurations
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/frontend-deployment.yaml
kubectl apply -f k8s/ingress.yaml

# Check deployment status
kubectl get pods -n puzzle-platform
kubectl get services -n puzzle-platform
kubectl get ingress -n puzzle-platform
```

## Step 5: Database Migration

### Run EF Core Migrations
```bash
# Build migration bundle
dotnet ef migrations bundle \
  --project src/CollaborativePuzzle.Infrastructure \
  --startup-project src/CollaborativePuzzle.Api \
  --output efbundle

# Run migrations
./efbundle --connection "Server=tcp:${SQL_SERVER}.database.windows.net,1433;Database=${SQL_DATABASE};User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};"
```

### Execute Stored Procedures
```sql
-- Connect to Azure SQL Database and run stored procedure scripts
-- Example: sp_CreatePuzzle, sp_GetPuzzleWithPieces, etc.
```

## Step 6: Monitoring Setup

### Configure Application Insights
```bash
# Get instrumentation key
APP_INSIGHTS_KEY=$(az monitor app-insights component show \
  --resource-group $RESOURCE_GROUP \
  --app puzzle-platform \
  --query instrumentationKey -o tsv)

# Update ConfigMap with key
kubectl create configmap app-insights \
  --from-literal=InstrumentationKey=$APP_INSIGHTS_KEY \
  -n puzzle-platform
```

### Setup Prometheus Monitoring
```yaml
# k8s/prometheus-servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: puzzle-api-metrics
  namespace: puzzle-platform
spec:
  selector:
    matchLabels:
      app: puzzle-api
  endpoints:
  - port: metrics
    interval: 30s
    path: /metrics
```

## Step 7: Autoscaling Configuration

### Horizontal Pod Autoscaler
```yaml
# k8s/hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: puzzle-api-hpa
  namespace: puzzle-platform
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
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 100
        periodSeconds: 60
```

### Cluster Autoscaler
```bash
# Enable cluster autoscaler
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --enable-cluster-autoscaler \
  --min-count 3 \
  --max-count 10
```

## Step 8: Security Hardening

### Network Policies
```yaml
# k8s/network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: puzzle-api-network-policy
  namespace: puzzle-platform
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
    - podSelector:
        matchLabels:
          app: puzzle-frontend
    ports:
    - protocol: TCP
      port: 80
  egress:
  - to:
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 1433  # SQL Server
    - protocol: TCP
      port: 6380  # Redis SSL
    - protocol: TCP
      port: 443   # HTTPS
```

### Pod Security Policy
```yaml
# k8s/pod-security-policy.yaml
apiVersion: policy/v1beta1
kind: PodSecurityPolicy
metadata:
  name: puzzle-platform-psp
spec:
  privileged: false
  allowPrivilegeEscalation: false
  requiredDropCapabilities:
  - ALL
  volumes:
  - 'configMap'
  - 'emptyDir'
  - 'projected'
  - 'secret'
  - 'downwardAPI'
  - 'persistentVolumeClaim'
  runAsUser:
    rule: 'MustRunAsNonRoot'
  seLinux:
    rule: 'RunAsAny'
  fsGroup:
    rule: 'RunAsAny'
  readOnlyRootFilesystem: true
```

## Step 9: CI/CD Pipeline

### Azure DevOps Pipeline
```yaml
# azure-pipelines.yml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

variables:
  dockerRegistryServiceConnection: 'acr-connection'
  imageRepository: 'puzzle-api'
  containerRegistry: 'acrpuzzleplatform.azurecr.io'
  dockerfilePath: '$(Build.SourcesDirectory)/Dockerfile'
  tag: '$(Build.BuildId)'

stages:
- stage: Build
  displayName: Build and push stage
  jobs:
  - job: Build
    displayName: Build
    steps:
    - task: Docker@2
      displayName: Build and push API image
      inputs:
        command: buildAndPush
        repository: $(imageRepository)
        dockerfile: $(dockerfilePath)
        containerRegistry: $(dockerRegistryServiceConnection)
        tags: |
          $(tag)
          latest

- stage: Deploy
  displayName: Deploy to AKS
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: Deploy
    displayName: Deploy
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: KubernetesManifest@0
            displayName: Deploy to Kubernetes cluster
            inputs:
              action: deploy
              manifests: |
                $(Pipeline.Workspace)/k8s/*.yaml
              containers: |
                $(containerRegistry)/$(imageRepository):$(tag)
```

## Step 10: Post-Deployment Tasks

### Verify Deployment
```bash
# Check pod status
kubectl get pods -n puzzle-platform

# Check logs
kubectl logs -f deployment/puzzle-api -n puzzle-platform

# Test endpoints
curl https://puzzle.yourdomain.com/health
curl https://puzzle.yourdomain.com/api/puzzles

# Run smoke tests
./scripts/smoke-tests.sh
```

### Configure Alerts
```bash
# Create action group
az monitor action-group create \
  --resource-group $RESOURCE_GROUP \
  --name "PuzzleAlerts" \
  --short-name "PuzzleAlert" \
  --email-receiver admin-email="admin@yourdomain.com"

# Create metric alerts
az monitor metrics alert create \
  --resource-group $RESOURCE_GROUP \
  --name "HighCPU" \
  --target $AKS_CLUSTER \
  --condition "avg cpu percentage > 80" \
  --window-size 5m \
  --action "PuzzleAlerts"
```

### Backup Configuration
```bash
# Backup SQL Database
az sql db backup \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DATABASE \
  --storage-key $STORAGE_KEY \
  --storage-key-type StorageAccessKey \
  --storage-uri https://${STORAGE_ACCOUNT}.blob.core.windows.net/backups/
```

## Maintenance Procedures

### Rolling Updates
```bash
# Update image
kubectl set image deployment/puzzle-api \
  api=${ACR_NAME}.azurecr.io/puzzle-api:v2.0 \
  -n puzzle-platform

# Monitor rollout
kubectl rollout status deployment/puzzle-api -n puzzle-platform

# Rollback if needed
kubectl rollout undo deployment/puzzle-api -n puzzle-platform
```

### Scaling Operations
```bash
# Manual scaling
kubectl scale deployment/puzzle-api --replicas=5 -n puzzle-platform

# Update autoscaler
kubectl edit hpa puzzle-api-hpa -n puzzle-platform
```

## Troubleshooting

### Common Issues

1. **Pods not starting**
```bash
kubectl describe pod <pod-name> -n puzzle-platform
kubectl logs <pod-name> -n puzzle-platform --previous
```

2. **Database connection issues**
```bash
# Test connection from pod
kubectl exec -it <pod-name> -n puzzle-platform -- /bin/bash
apt-get update && apt-get install -y mssql-tools
sqlcmd -S sql-puzzle-platform.database.windows.net -U sqladmin -P $PASSWORD
```

3. **SignalR connection failures**
```bash
# Check WebSocket support
kubectl logs -l app=puzzle-api -n puzzle-platform | grep -i websocket
```

## Summary

This deployment guide covers:
- Infrastructure provisioning on Azure
- Container image building and registry setup
- Kubernetes deployment configuration
- Security hardening
- Monitoring and alerting
- CI/CD pipeline setup
- Maintenance procedures

Following these steps will result in a production-ready deployment of the Collaborative Puzzle Platform on Azure Kubernetes Service.