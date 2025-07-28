# Containers, Cloud, and Terraform Primer

## From Local Docker to Multi-Cloud Infrastructure as Code

### Table of Contents
1. [Container Fundamentals](#container-fundamentals)
2. [Local Development with Docker](#local-development-with-docker)
3. [Container Registries](#container-registries)
4. [Kubernetes Orchestration](#kubernetes-orchestration)
5. [Azure Container Services](#azure-container-services)
6. [AWS Container Services](#aws-container-services)
7. [GCP Container Services](#gcp-container-services)
8. [Terraform Fundamentals](#terraform-fundamentals)
9. [Multi-Cloud Container Deployment](#multi-cloud-container-deployment)
10. [Best Practices and Patterns](#best-practices-and-patterns)

## Container Fundamentals

### What are Containers?

Containers are lightweight, standalone, executable packages that include everything needed to run software: code, runtime, system tools, libraries, and settings.

```
Traditional VM Architecture:          Container Architecture:
┌─────────────────────┐              ┌─────────────────────┐
│    Application     │              │    Application      │
├─────────────────────┤              ├─────────────────────┤
│    Guest OS         │              │    Container        │
├─────────────────────┤              ├─────────────────────┤
│    Hypervisor      │              │   Container Engine  │
├─────────────────────┤              ├─────────────────────┤
│    Host OS          │              │    Host OS          │
├─────────────────────┤              ├─────────────────────┤
│    Hardware         │              │    Hardware         │
└─────────────────────┘              └─────────────────────┘
```

### Key Concepts

```yaml
Container Image:
  - Read-only template
  - Layered filesystem
  - Contains application and dependencies
  - Immutable once built

Container Instance:
  - Running instance of an image
  - Isolated process
  - Has its own filesystem, network, process tree
  - Ephemeral by design

Container Registry:
  - Storage for container images
  - Version control for containers
  - Distribution mechanism
```

## Local Development with Docker

### Docker Architecture

```yaml
Docker Components:
  Docker Client:
    - CLI tool (docker command)
    - Communicates with daemon
    
  Docker Daemon:
    - Background service (dockerd)
    - Manages containers, images, networks
    
  Docker Registry:
    - Stores Docker images
    - Docker Hub is default public registry
```

### Dockerfile Best Practices

```dockerfile
# Multi-stage build for .NET application
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only csproj first (layer caching)
COPY ["src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj", "CollaborativePuzzle.Api/"]
COPY ["src/CollaborativePuzzle.Core/CollaborativePuzzle.Core.csproj", "CollaborativePuzzle.Core/"]
RUN dotnet restore "CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj"

# Copy rest of code
COPY src/ .
WORKDIR "/src/CollaborativePuzzle.Api"
RUN dotnet build -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Security: Run as non-root user
RUN useradd -m -u 1001 appuser && \
    chown -R appuser:appuser /app
USER appuser

# Copy from publish stage
COPY --from=publish --chown=appuser:appuser /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Expose port (documentation only)
EXPOSE 8080

# Use exec form to ensure PID 1
ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]
```

### Docker Compose for Local Development

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
      target: final
      args:
        - BUILD_VERSION=${VERSION:-1.0.0}
    image: collaborative-puzzle:${VERSION:-latest}
    container_name: puzzle-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=db;Database=PuzzleDb;User=sa;Password=${DB_PASSWORD}
      - Redis__ConnectionString=redis:6379
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - puzzle-net
    volumes:
      - ./appsettings.Development.json:/app/appsettings.Development.json:ro
      - puzzle-data:/app/data
    restart: unless-stopped

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: puzzle-db
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${DB_PASSWORD}
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - db-data:/var/opt/mssql
    networks:
      - puzzle-net
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${DB_PASSWORD} -Q "SELECT 1"
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 10s

  redis:
    image: redis:7-alpine
    container_name: puzzle-redis
    command: redis-server --appendonly yes
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - puzzle-net
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

networks:
  puzzle-net:
    driver: bridge

volumes:
  puzzle-data:
  db-data:
  redis-data:
```

### Development Workflow

```bash
# Build and run locally
docker-compose up --build

# Run in background
docker-compose up -d

# View logs
docker-compose logs -f api

# Execute commands in container
docker-compose exec api dotnet ef database update

# Clean up
docker-compose down -v  # -v removes volumes
```

## Container Registries

### Registry Comparison

| Registry | Best For | Features | Pricing |
|----------|----------|----------|---------|
| Docker Hub | Public images | CI/CD integration, Auto-builds | Free tier limited |
| Azure Container Registry | Azure deployments | Geo-replication, Azure AD | Pay per GB |
| Amazon ECR | AWS deployments | IAM integration, Scanning | Pay per GB |
| Google Container Registry | GCP deployments | Vulnerability scanning | Pay per GB |
| GitHub Container Registry | Open source | GitHub integration | Free for public |
| Harbor | Self-hosted | Security scanning, RBAC | Self-hosted |

### Multi-Registry Strategy

```bash
# Tag for multiple registries
docker tag myapp:latest myregistry.azurecr.io/myapp:latest
docker tag myapp:latest 123456789.dkr.ecr.us-east-1.amazonaws.com/myapp:latest
docker tag myapp:latest gcr.io/myproject/myapp:latest

# Push to multiple registries
docker push myregistry.azurecr.io/myapp:latest
docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/myapp:latest
docker push gcr.io/myproject/myapp:latest
```

## Kubernetes Orchestration

### Kubernetes Architecture

```
┌─────────────────────────────────────────────────┐
│                 Master Node                      │
│  ┌─────────┐ ┌──────────┐ ┌────────────────┐  │
│  │   API   │ │Scheduler │ │Controller      │  │
│  │ Server  │ │          │ │Manager         │  │
│  └─────────┘ └──────────┘ └────────────────┘  │
│  ┌─────────────────────────────────────────┐  │
│  │              etcd                        │  │
│  └─────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
                        │
    ┌───────────────────┼───────────────────┐
    │                   │                   │
┌───▼─────────┐ ┌───────▼────────┐ ┌───────▼────────┐
│ Worker Node │ │  Worker Node   │ │  Worker Node   │
│ ┌─────────┐ │ │ ┌─────────┐   │ │ ┌─────────┐   │
│ │ kubelet │ │ │ │ kubelet │   │ │ │ kubelet │   │
│ └─────────┘ │ │ └─────────┘   │ │ └─────────┘   │
│ ┌─────────┐ │ │ ┌─────────┐   │ │ ┌─────────┐   │
│ │  Pods   │ │ │ │  Pods   │   │ │ │  Pods   │   │
│ └─────────┘ │ │ └─────────┘   │ │ └─────────┘   │
└─────────────┘ └────────────────┘ └────────────────┘
```

### Kubernetes Manifests

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: puzzle-api
  namespace: production
  labels:
    app: puzzle-api
    version: v1
spec:
  replicas: 3
  selector:
    matchLabels:
      app: puzzle-api
  template:
    metadata:
      labels:
        app: puzzle-api
        version: v1
    spec:
      containers:
      - name: api
        image: myregistry.azurecr.io/puzzle-api:1.0.0
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: puzzle-secrets
              key: db-connection
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
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        volumeMounts:
        - name: config
          mountPath: /app/config
          readOnly: true
      volumes:
      - name: config
        configMap:
          name: puzzle-config
---
# service.yaml
apiVersion: v1
kind: Service
metadata:
  name: puzzle-api
  namespace: production
spec:
  selector:
    app: puzzle-api
  ports:
  - name: http
    port: 80
    targetPort: 8080
  type: LoadBalancer
---
# hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: puzzle-api-hpa
  namespace: production
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
```

## Azure Container Services

### Azure Container Instances (ACI)

```bash
# Quick deployment
az container create \
  --resource-group myResourceGroup \
  --name puzzle-api \
  --image myregistry.azurecr.io/puzzle-api:latest \
  --cpu 1 \
  --memory 1 \
  --registry-login-server myregistry.azurecr.io \
  --registry-username <username> \
  --registry-password <password> \
  --dns-name-label puzzle-api \
  --ports 80
```

### Azure Kubernetes Service (AKS)

```bash
# Create AKS cluster
az aks create \
  --resource-group myResourceGroup \
  --name myAKSCluster \
  --node-count 3 \
  --enable-addons monitoring \
  --generate-ssh-keys \
  --attach-acr myregistry

# Get credentials
az aks get-credentials \
  --resource-group myResourceGroup \
  --name myAKSCluster

# Deploy application
kubectl apply -f k8s/
```

### Azure Container Apps

```bash
# Create Container App
az containerapp create \
  --name puzzle-api \
  --resource-group myResourceGroup \
  --environment myEnvironment \
  --image myregistry.azurecr.io/puzzle-api:latest \
  --target-port 8080 \
  --ingress 'external' \
  --min-replicas 1 \
  --max-replicas 10 \
  --cpu 0.5 \
  --memory 1.0Gi
```

## AWS Container Services

### Amazon ECS (Elastic Container Service)

```json
// task-definition.json
{
  "family": "puzzle-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "containerDefinitions": [
    {
      "name": "puzzle-api",
      "image": "123456789.dkr.ecr.us-east-1.amazonaws.com/puzzle-api:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:123456789:secret:db-connection"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/puzzle-api",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      },
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3
      }
    }
  ]
}
```

### Amazon EKS (Elastic Kubernetes Service)

```bash
# Create EKS cluster using eksctl
eksctl create cluster \
  --name puzzle-cluster \
  --region us-east-1 \
  --nodegroup-name standard-workers \
  --node-type t3.medium \
  --nodes 3 \
  --nodes-min 1 \
  --nodes-max 5 \
  --managed

# Deploy application
kubectl apply -f k8s/
```

### AWS App Runner

```yaml
# apprunner.yaml
version: 1.0
runtime: docker
build:
  commands:
    build:
      - echo "No build commands"
run:
  runtime-version: latest
  command: dotnet CollaborativePuzzle.Api.dll
  network:
    port: 8080
    env: PORT
  env:
    - name: ASPNETCORE_ENVIRONMENT
      value: "Production"
```

## GCP Container Services

### Google Kubernetes Engine (GKE)

```bash
# Create GKE cluster
gcloud container clusters create puzzle-cluster \
  --zone us-central1-a \
  --num-nodes 3 \
  --enable-autoscaling \
  --min-nodes 1 \
  --max-nodes 10 \
  --enable-autorepair \
  --enable-autoupgrade

# Get credentials
gcloud container clusters get-credentials puzzle-cluster \
  --zone us-central1-a

# Deploy application
kubectl apply -f k8s/
```

### Cloud Run

```bash
# Deploy to Cloud Run
gcloud run deploy puzzle-api \
  --image gcr.io/myproject/puzzle-api:latest \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars "ASPNETCORE_ENVIRONMENT=Production" \
  --set-secrets "ConnectionStrings__DefaultConnection=db-connection:latest" \
  --memory 1Gi \
  --cpu 1 \
  --min-instances 1 \
  --max-instances 100
```

## Terraform Fundamentals

### Terraform Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ Terraform    │────▶│ Terraform    │────▶│ Cloud        │
│ Configuration│     │ Core         │     │ Providers    │
└──────────────┘     └──────────────┘     └──────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │ State File   │
                     └──────────────┘
```

### Basic Terraform Structure

```hcl
# main.tf
terraform {
  required_version = ">= 1.0"
  
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
  
  backend "s3" {
    bucket = "my-terraform-state"
    key    = "puzzle-app/terraform.tfstate"
    region = "us-east-1"
  }
}

# variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
  default     = "development"
}

variable "app_name" {
  description = "Application name"
  type        = string
  default     = "puzzle-api"
}

variable "regions" {
  description = "Deployment regions by cloud"
  type = object({
    azure = string
    aws   = string
    gcp   = string
  })
  default = {
    azure = "eastus"
    aws   = "us-east-1"
    gcp   = "us-central1"
  }
}

# outputs.tf
output "endpoints" {
  description = "Application endpoints by cloud"
  value = {
    azure = module.azure_deployment.endpoint
    aws   = module.aws_deployment.endpoint
    gcp   = module.gcp_deployment.endpoint
  }
}
```

## Multi-Cloud Container Deployment

### Azure Deployment with Terraform

```hcl
# azure/main.tf
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "${var.app_name}-${var.environment}-rg"
  location = var.azure_region
}

resource "azurerm_container_registry" "acr" {
  name                = "${var.app_name}${var.environment}acr"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Standard"
  admin_enabled       = false
}

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "${var.app_name}-${var.environment}-aks"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "${var.app_name}-${var.environment}"

  default_node_pool {
    name       = "default"
    node_count = var.node_count
    vm_size    = "Standard_D2_v2"
    
    enable_auto_scaling = true
    min_count          = var.min_nodes
    max_count          = var.max_nodes
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin    = "azure"
    load_balancer_sku = "standard"
  }
}

# Grant AKS access to ACR
resource "azurerm_role_assignment" "aks_acr" {
  principal_id                     = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
  role_definition_name             = "AcrPull"
  scope                           = azurerm_container_registry.acr.id
  skip_service_principal_aad_check = true
}
```

### AWS Deployment with Terraform

```hcl
# aws/main.tf
provider "aws" {
  region = var.aws_region
}

resource "aws_ecr_repository" "main" {
  name                 = "${var.app_name}-${var.environment}"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecs_cluster" "main" {
  name = "${var.app_name}-${var.environment}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_task_definition" "app" {
  family                   = "${var.app_name}-${var.environment}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.cpu
  memory                   = var.memory
  execution_role_arn       = aws_iam_role.ecs_execution_role.arn
  task_role_arn            = aws_iam_role.ecs_task_role.arn

  container_definitions = jsonencode([
    {
      name  = var.app_name
      image = "${aws_ecr_repository.main.repository_url}:latest"
      
      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]
      
      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment
        }
      ]
      
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.main.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "main" {
  name            = "${var.app_name}-${var.environment}-service"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.app.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    security_groups  = [aws_security_group.ecs_tasks.id]
    subnets          = aws_subnet.private[*].id
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_alb_target_group.app.id
    container_name   = var.app_name
    container_port   = 8080
  }
}
```

### GCP Deployment with Terraform

```hcl
# gcp/main.tf
provider "google" {
  project = var.gcp_project
  region  = var.gcp_region
}

resource "google_artifact_registry_repository" "main" {
  location      = var.gcp_region
  repository_id = "${var.app_name}-${var.environment}"
  format        = "DOCKER"
}

resource "google_container_cluster" "main" {
  name     = "${var.app_name}-${var.environment}-gke"
  location = var.gcp_zone

  # We can't create a cluster with no node pool defined, but we want to only use
  # separately managed node pools. So we create the smallest possible default
  # node pool and immediately delete it.
  remove_default_node_pool = true
  initial_node_count       = 1

  workload_identity_config {
    workload_pool = "${var.gcp_project}.svc.id.goog"
  }
}

resource "google_container_node_pool" "primary_nodes" {
  name       = "${var.app_name}-${var.environment}-node-pool"
  location   = var.gcp_zone
  cluster    = google_container_cluster.main.name
  node_count = var.node_count

  autoscaling {
    min_node_count = var.min_nodes
    max_node_count = var.max_nodes
  }

  node_config {
    preemptible  = var.environment != "production"
    machine_type = "e2-medium"

    workload_metadata_config {
      mode = "GKE_METADATA"
    }

    oauth_scopes = [
      "https://www.googleapis.com/auth/cloud-platform"
    ]
  }
}

# Cloud Run Alternative
resource "google_cloud_run_service" "main" {
  name     = "${var.app_name}-${var.environment}"
  location = var.gcp_region

  template {
    spec {
      containers {
        image = "${var.gcp_region}-docker.pkg.dev/${var.gcp_project}/${google_artifact_registry_repository.main.repository_id}/${var.app_name}:latest"
        
        resources {
          limits = {
            cpu    = "1000m"
            memory = "1024Mi"
          }
        }
        
        env {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment
        }
      }
    }
    
    metadata {
      annotations = {
        "autoscaling.knative.dev/maxScale" = "100"
        "autoscaling.knative.dev/minScale" = "1"
      }
    }
  }

  traffic {
    percent         = 100
    latest_revision = true
  }
}
```

### Unified Multi-Cloud Module

```hcl
# modules/container-app/main.tf
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

variable "cloud_provider" {
  description = "Cloud provider to deploy to"
  type        = string
  validation {
    condition     = contains(["azure", "aws", "gcp"], var.cloud_provider)
    error_message = "Cloud provider must be azure, aws, or gcp."
  }
}

# Conditional resource creation based on cloud provider
resource "azurerm_container_group" "main" {
  count = var.cloud_provider == "azure" ? 1 : 0
  # Azure-specific configuration
}

resource "aws_ecs_service" "main" {
  count = var.cloud_provider == "aws" ? 1 : 0
  # AWS-specific configuration
}

resource "google_cloud_run_service" "main" {
  count = var.cloud_provider == "gcp" ? 1 : 0
  # GCP-specific configuration
}

output "endpoint" {
  description = "Application endpoint"
  value = coalesce(
    try(azurerm_container_group.main[0].fqdn, ""),
    try(aws_alb.main[0].dns_name, ""),
    try(google_cloud_run_service.main[0].status[0].url, "")
  )
}
```

## Best Practices and Patterns

### Container Security

```dockerfile
# Security scanning in CI/CD
# .github/workflows/security.yml
name: Security Scan
on: [push]

jobs:
  scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Run Trivy vulnerability scanner
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: 'myapp:latest'
          format: 'sarif'
          output: 'trivy-results.sarif'
          
      - name: Upload Trivy scan results
        uses: github/codeql-action/upload-sarif@v2
        with:
          sarif_file: 'trivy-results.sarif'
```

### Multi-Environment Configuration

```hcl
# environments/dev.tfvars
environment = "development"
node_count  = 1
min_nodes   = 1
max_nodes   = 3
cpu         = "256"
memory      = "512"

# environments/prod.tfvars
environment = "production"
node_count  = 3
min_nodes   = 3
max_nodes   = 10
cpu         = "1024"
memory      = "2048"
```

### Cost Optimization

```hcl
# Spot/Preemptible instances for non-production
resource "aws_eks_node_group" "spot" {
  count = var.environment != "production" ? 1 : 0
  
  capacity_type = "SPOT"
  instance_types = ["t3.medium", "t3a.medium"]
  
  scaling_config {
    desired_size = 2
    max_size     = 5
    min_size     = 1
  }
}

# Auto-shutdown for development environments
resource "aws_lambda_function" "auto_shutdown" {
  count = var.environment == "development" ? 1 : 0
  
  filename      = "auto_shutdown.zip"
  function_name = "${var.app_name}-auto-shutdown"
  role          = aws_iam_role.lambda_role.arn
  handler       = "index.handler"
  runtime       = "python3.9"
  
  environment {
    variables = {
      CLUSTER_NAME = aws_eks_cluster.main.name
    }
  }
}

resource "aws_cloudwatch_event_rule" "shutdown_schedule" {
  count = var.environment == "development" ? 1 : 0
  
  name                = "${var.app_name}-shutdown-schedule"
  description         = "Shutdown development environment at night"
  schedule_expression = "cron(0 20 * * ? *)"  # 8 PM daily
}
```

### GitOps Workflow

```yaml
# .github/workflows/deploy.yml
name: Deploy to Multi-Cloud
on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    outputs:
      image-tag: ${{ steps.meta.outputs.tags }}
    steps:
      - uses: actions/checkout@v3
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2
        
      - name: Login to registries
        run: |
          echo ${{ secrets.ACR_PASSWORD }} | docker login ${{ vars.ACR_REGISTRY }} -u ${{ secrets.ACR_USERNAME }} --password-stdin
          echo ${{ secrets.AWS_PASSWORD }} | docker login ${{ vars.ECR_REGISTRY }} -u AWS --password-stdin
          echo ${{ secrets.GCP_KEY }} | docker login -u _json_key --password-stdin https://gcr.io
          
      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: .
          push: true
          tags: |
            ${{ vars.ACR_REGISTRY }}/myapp:${{ github.sha }}
            ${{ vars.ECR_REGISTRY }}/myapp:${{ github.sha }}
            gcr.io/${{ vars.GCP_PROJECT }}/myapp:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy:
    needs: build
    runs-on: ubuntu-latest
    strategy:
      matrix:
        cloud: [azure, aws, gcp]
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v2
        
      - name: Terraform Deploy
        run: |
          cd terraform/${{ matrix.cloud }}
          terraform init
          terraform apply -auto-approve \
            -var="image_tag=${{ github.sha }}" \
            -var="environment=production"
```

### Monitoring and Observability

```hcl
# Unified monitoring across clouds
module "monitoring" {
  source = "./modules/monitoring"
  
  providers = {
    azurerm = azurerm
    aws     = aws
    google  = google
  }
  
  app_name    = var.app_name
  environment = var.environment
  
  # Datadog integration
  datadog_api_key = var.datadog_api_key
  datadog_app_key = var.datadog_app_key
  
  # Prometheus endpoints
  prometheus_endpoints = {
    azure = module.azure_deployment.prometheus_endpoint
    aws   = module.aws_deployment.prometheus_endpoint
    gcp   = module.gcp_deployment.prometheus_endpoint
  }
}
```

## Summary

### Key Takeaways

1. **Start Local**: Master Docker and Docker Compose before cloud
2. **Choose Wisely**: Each cloud has strengths - pick based on needs
3. **Standardize**: Use Kubernetes for multi-cloud portability
4. **Automate Everything**: Terraform for infrastructure, GitOps for deployments
5. **Security First**: Scan images, use least privilege, encrypt secrets
6. **Monitor Costs**: Use spot instances, auto-scaling, and shutdown policies
7. **Plan for Failure**: Multi-region, multi-cloud for true resilience
8. **Keep It Simple**: Start small, grow complexity as needed

### Decision Matrix

```yaml
Choose Containers When:
  - Need consistent environments
  - Want faster deployments
  - Require microservices architecture
  - Need to scale dynamically

Choose Kubernetes When:
  - Managing multiple containers
  - Need advanced orchestration
  - Want self-healing systems
  - Require complex networking

Choose Terraform When:
  - Managing multi-cloud infrastructure
  - Need reproducible environments
  - Want infrastructure versioning
  - Require compliance documentation

Choose Multi-Cloud When:
  - Need vendor independence
  - Require geographic distribution
  - Want best-of-breed services
  - Have regulatory requirements
```