# Docker Compose and Docker CLI Primer
## Mastering Container Development Workflows

### Executive Summary

Docker has revolutionized software development by providing consistent environments from development to production. This primer covers Docker CLI fundamentals and Docker Compose orchestration, enabling developers to efficiently manage containerized applications.

## Table of Contents

1. [Docker CLI Fundamentals](#docker-cli-fundamentals)
2. [Image Management](#image-management)
3. [Container Operations](#container-operations)
4. [Docker Compose Basics](#docker-compose-basics)
5. [Multi-Service Applications](#multi-service-applications)
6. [Networking and Volumes](#networking-and-volumes)
7. [Environment Management](#environment-management)
8. [Production Considerations](#production-considerations)
9. [Debugging and Troubleshooting](#debugging-and-troubleshooting)
10. [Best Practices](#best-practices)

## Docker CLI Fundamentals

### Essential Commands Structure

```bash
docker [OPTIONS] COMMAND [ARG...]
docker [OPTIONS] COMMAND SUBCOMMAND [ARG...]
```

### Core Docker Commands

```bash
# System Information
docker version              # Show Docker version info
docker info                 # Display system-wide information
docker system df            # Show Docker disk usage
docker system prune -a      # Remove unused data

# Docker Hub Authentication
docker login                # Log in to Docker Hub
docker logout               # Log out from Docker Hub
```

### Docker Context

```bash
# Managing Docker contexts for remote Docker daemons
docker context create remote --docker "host=ssh://user@remote-host"
docker context use remote
docker context ls
```

## Image Management

### Building Images

```bash
# Basic build
docker build -t myapp:latest .

# Build with build arguments
docker build --build-arg VERSION=1.0.0 -t myapp:1.0.0 .

# Build with custom Dockerfile
docker build -f Dockerfile.prod -t myapp:prod .

# Build with no cache
docker build --no-cache -t myapp:latest .

# Multi-stage build target
docker build --target production -t myapp:prod .

# Build with progress output
docker build --progress=plain -t myapp:latest .
```

### Advanced Dockerfile

```dockerfile
# Multi-stage build with caching optimization
# syntax=docker/dockerfile:1.4

# Base stage for dependencies
FROM node:18-alpine AS base
WORKDIR /app
COPY package*.json ./
# Mount cache for npm packages
RUN --mount=type=cache,target=/root/.npm \
    npm ci --only=production

# Development dependencies
FROM base AS dev-deps
RUN --mount=type=cache,target=/root/.npm \
    npm ci

# Build stage
FROM dev-deps AS build
COPY . .
RUN npm run build

# Production stage
FROM base AS production
COPY --from=build /app/dist ./dist
EXPOSE 3000
USER node
CMD ["node", "dist/index.js"]

# Development stage
FROM dev-deps AS development
COPY . .
EXPOSE 3000
CMD ["npm", "run", "dev"]
```

### Image Operations

```bash
# List images
docker images
docker image ls --filter "dangling=true"

# Pull images
docker pull nginx:latest
docker pull myregistry.com/myapp:1.0.0

# Push images
docker tag myapp:latest myregistry.com/myapp:1.0.0
docker push myregistry.com/myapp:1.0.0

# Save and load images
docker save myapp:latest | gzip > myapp.tar.gz
docker load < myapp.tar.gz

# Export and import (flattened)
docker export container_id | gzip > container.tar.gz
docker import container.tar.gz myapp:imported

# Image inspection
docker image inspect nginx:latest
docker history nginx:latest --no-trunc

# Remove images
docker rmi myapp:old
docker image prune -a --filter "until=24h"
```

## Container Operations

### Running Containers

```bash
# Basic run
docker run nginx

# Run with options
docker run -d \                      # Detached mode
  --name webserver \                 # Container name
  -p 8080:80 \                      # Port mapping
  -e NODE_ENV=production \          # Environment variable
  --restart unless-stopped \        # Restart policy
  nginx:latest

# Run with resource limits
docker run -d \
  --memory="1g" \                   # Memory limit
  --cpus="0.5" \                    # CPU limit
  --memory-reservation="750m" \     # Soft memory limit
  myapp:latest

# Run with volumes
docker run -d \
  -v $(pwd)/data:/app/data \        # Bind mount
  -v app-config:/app/config \       # Named volume
  --mount type=tmpfs,destination=/tmp \ # tmpfs mount
  myapp:latest

# Run with custom network
docker run -d \
  --network mynetwork \
  --ip 172.20.0.10 \
  --add-host db:172.20.0.20 \
  myapp:latest
```

### Container Management

```bash
# List containers
docker ps                           # Running containers
docker ps -a                        # All containers
docker ps --filter "status=exited"  # Filtered list

# Container lifecycle
docker start container_name
docker stop container_name
docker restart container_name
docker pause container_name
docker unpause container_name

# Container interaction
docker exec -it container_name bash
docker exec container_name env
docker attach container_name        # Attach to running container

# Container logs
docker logs container_name
docker logs -f container_name       # Follow logs
docker logs --tail 50 container_name
docker logs --since 2023-01-01 container_name

# Copy files
docker cp container_name:/app/logs/app.log ./
docker cp ./config.json container_name:/app/config/

# Container inspection
docker inspect container_name
docker stats                        # Real-time resource usage
docker top container_name          # Running processes

# Container cleanup
docker rm container_name
docker container prune
```

## Docker Compose Basics

### Compose File Structure

```yaml
# docker-compose.yml
version: '3.9'

services:
  web:
    build: .
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=development
    depends_on:
      - db
      - redis

  db:
    image: postgres:14
    environment:
      POSTGRES_PASSWORD: secret
    volumes:
      - db-data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes

volumes:
  db-data:

networks:
  default:
    driver: bridge
```

### Compose Commands

```bash
# Basic operations
docker-compose up                   # Start services
docker-compose up -d               # Start in background
docker-compose down                # Stop and remove
docker-compose down -v             # Also remove volumes

# Service management
docker-compose start               # Start existing containers
docker-compose stop                # Stop running containers
docker-compose restart web         # Restart specific service
docker-compose pause               # Pause all services
docker-compose unpause             # Unpause all services

# Logs and monitoring
docker-compose logs                # View all logs
docker-compose logs -f web         # Follow specific service
docker-compose ps                  # List containers
docker-compose top                 # Display running processes

# Building
docker-compose build               # Build all services
docker-compose build --no-cache web # Rebuild specific service
docker-compose pull                # Pull all images

# Scaling
docker-compose up -d --scale web=3 # Scale service instances

# Execution
docker-compose exec web bash       # Execute command in service
docker-compose run web npm test    # Run one-off command
```

## Multi-Service Applications

### Complex Application Stack

```yaml
# docker-compose.yml for microservices architecture
version: '3.9'

x-common-variables: &common-variables
  LOG_LEVEL: debug
  REDIS_URL: redis://redis:6379

services:
  # API Gateway
  gateway:
    build:
      context: ./gateway
      dockerfile: Dockerfile
      args:
        - BUILD_VERSION=${VERSION:-latest}
    ports:
      - "8080:8080"
    environment:
      <<: *common-variables
      SERVICE_DISCOVERY_URL: http://consul:8500
    depends_on:
      consul:
        condition: service_healthy
    networks:
      - frontend
      - backend

  # Authentication Service
  auth:
    build: ./services/auth
    environment:
      <<: *common-variables
      JWT_SECRET_FILE: /run/secrets/jwt_secret
      DATABASE_URL: postgresql://user:pass@auth-db:5432/auth
    secrets:
      - jwt_secret
    depends_on:
      auth-db:
        condition: service_healthy
      redis:
        condition: service_started
    networks:
      - backend

  # Auth Database
  auth-db:
    image: postgres:14-alpine
    environment:
      POSTGRES_USER: user
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
      POSTGRES_DB: auth
    secrets:
      - db_password
    volumes:
      - auth-db-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U user"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - backend

  # User Service
  user-service:
    build: ./services/user
    deploy:
      replicas: 2
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    environment:
      <<: *common-variables
    networks:
      - backend

  # Message Queue
  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports:
      - "15672:15672"  # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS_FILE: /run/secrets/rabbitmq_password
    secrets:
      - rabbitmq_password
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    networks:
      - backend

  # Cache
  redis:
    image: redis:7-alpine
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --maxmemory 256mb
      --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    networks:
      - backend

  # Service Discovery
  consul:
    image: consul:latest
    ports:
      - "8500:8500"
    command: agent -server -bootstrap-expect=1 -ui -client=0.0.0.0
    healthcheck:
      test: ["CMD", "consul", "info"]
      interval: 10s
      timeout: 5s
      retries: 3
    networks:
      - backend

  # Monitoring
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    ports:
      - "9090:9090"
    networks:
      - monitoring

  grafana:
    image: grafana/grafana:latest
    environment:
      GF_SECURITY_ADMIN_PASSWORD_FILE: /run/secrets/grafana_password
    secrets:
      - grafana_password
    volumes:
      - grafana-data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
    ports:
      - "3001:3000"
    depends_on:
      - prometheus
    networks:
      - monitoring
      - frontend

volumes:
  auth-db-data:
  rabbitmq-data:
  redis-data:
  prometheus-data:
  grafana-data:

networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
    internal: true
  monitoring:
    driver: bridge

secrets:
  jwt_secret:
    file: ./secrets/jwt_secret.txt
  db_password:
    file: ./secrets/db_password.txt
  rabbitmq_password:
    file: ./secrets/rabbitmq_password.txt
  grafana_password:
    file: ./secrets/grafana_password.txt
```

### Override Files

```yaml
# docker-compose.override.yml (for development)
version: '3.9'

services:
  gateway:
    build:
      target: development
    volumes:
      - ./gateway:/app
      - /app/node_modules
    environment:
      DEBUG: "true"
    command: npm run dev

  auth:
    volumes:
      - ./services/auth:/app
      - /app/node_modules
    environment:
      LOG_LEVEL: debug
    command: npm run dev

# docker-compose.prod.yml (for production)
version: '3.9'

services:
  gateway:
    build:
      target: production
    restart: always
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  auth:
    restart: always
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

### Using Override Files

```bash
# Development (uses docker-compose.yml + docker-compose.override.yml)
docker-compose up

# Production
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Testing
docker-compose -f docker-compose.yml -f docker-compose.test.yml run tests
```

## Networking and Volumes

### Network Configuration

```yaml
# Advanced networking configuration
version: '3.9'

services:
  app:
    image: myapp:latest
    networks:
      frontend:
        ipv4_address: 172.20.0.5
      backend:
        aliases:
          - api.local
          - myapp.local

networks:
  frontend:
    driver: bridge
    ipam:
      driver: default
      config:
        - subnet: 172.20.0.0/16
          gateway: 172.20.0.1

  backend:
    driver: bridge
    internal: true  # No external access
    ipam:
      config:
        - subnet: 172.21.0.0/16

  external_network:
    external: true
    name: existing_network
```

### Volume Management

```yaml
# Advanced volume configuration
version: '3.9'

services:
  database:
    image: postgres:14
    volumes:
      # Named volume
      - db-data:/var/lib/postgresql/data
      # Bind mount with options
      - type: bind
        source: ./backup
        target: /backup
        read_only: true
      # tmpfs mount
      - type: tmpfs
        target: /tmp
        tmpfs:
          size: 100m
      # Volume with driver options
      - nfs-data:/shared

volumes:
  db-data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /data/postgres

  nfs-data:
    driver: local
    driver_opts:
      type: nfs
      o: addr=nfs-server.local,rw,nfsvers=4
      device: ":/exports/data"
```

### Network Commands

```bash
# Network management
docker network create mynetwork --driver bridge
docker network create --subnet=172.30.0.0/16 custom-network
docker network ls
docker network inspect bridge
docker network connect mynetwork container_name
docker network disconnect mynetwork container_name
docker network prune

# Volume management
docker volume create myvolume
docker volume ls
docker volume inspect myvolume
docker volume rm myvolume
docker volume prune
```

## Environment Management

### Environment Variables

```yaml
# Multiple ways to set environment variables
version: '3.9'

services:
  app:
    image: myapp:latest
    # Inline environment variables
    environment:
      NODE_ENV: production
      API_KEY: ${API_KEY}  # From shell environment
    
    # Environment file
    env_file:
      - .env
      - .env.production
    
    # Using .env file with variable substitution
    environment:
      DATABASE_URL: postgres://${DB_USER}:${DB_PASS}@db:5432/${DB_NAME}
```

### .env File Management

```bash
# .env (default values)
COMPOSE_PROJECT_NAME=myproject
DB_USER=postgres
DB_PASS=secret
DB_NAME=myapp

# .env.production
NODE_ENV=production
LOG_LEVEL=info
API_URL=https://api.production.com

# .env.development
NODE_ENV=development
LOG_LEVEL=debug
API_URL=http://localhost:8080
```

### Using env_file vs environment

```yaml
version: '3.9'

services:
  # env_file: loads file into container
  app1:
    image: myapp
    env_file:
      - ./app.env  # Contains: KEY=value pairs

  # environment: set in docker-compose.yml
  app2:
    image: myapp
    environment:
      - KEY=value
      - ANOTHER_KEY=${HOST_VARIABLE}
```

## Production Considerations

### Health Checks

```yaml
version: '3.9'

services:
  web:
    image: myapp:latest
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    deploy:
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
        window: 120s

  database:
    image: postgres:14
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
```

### Resource Limits

```yaml
version: '3.9'

services:
  app:
    image: myapp:latest
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
    # For non-swarm mode
    mem_limit: 512m
    mem_reservation: 256m
    cpus: 0.5
```

### Logging Configuration

```yaml
version: '3.9'

services:
  app:
    image: myapp:latest
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
        labels: "service=app,env=production"
        
  # Centralized logging
  log-aggregator:
    image: myapp:latest
    logging:
      driver: "fluentd"
      options:
        fluentd-address: "localhost:24224"
        tag: "app.{{.Name}}"
```

### Security Best Practices

```yaml
version: '3.9'

services:
  secure-app:
    image: myapp:latest
    # Read-only root filesystem
    read_only: true
    # Temporary filesystems for writable areas
    tmpfs:
      - /tmp
      - /var/run
    # Drop capabilities
    cap_drop:
      - ALL
    cap_add:
      - NET_BIND_SERVICE
    # Security options
    security_opt:
      - no-new-privileges:true
      - seccomp:unconfined
    # Run as non-root user
    user: "1000:1000"
    # Disable inter-container communication
    ipc: private
    # PID namespace
    pid: host
```

## Debugging and Troubleshooting

### Debugging Commands

```bash
# Container debugging
docker logs container_name --details
docker logs container_name 2>&1 | grep ERROR
docker events --filter container=container_name

# Process inspection
docker top container_name
docker exec container_name ps aux
docker exec container_name netstat -tulpn

# File system inspection
docker exec container_name ls -la /app
docker exec container_name cat /app/config.json
docker diff container_name  # Show filesystem changes

# Network debugging
docker exec container_name ping other_service
docker exec container_name nslookup other_service
docker exec container_name curl -v http://other_service/health

# Resource usage
docker stats --no-stream
docker system df
docker system events

# Compose debugging
docker-compose logs --tail=50 -f service_name
docker-compose exec service_name sh
docker-compose ps
docker-compose config  # Validate and view configuration
```

### Common Issues and Solutions

```bash
# Port already in use
lsof -i :8080  # Find process using port
docker-compose down  # Ensure containers are stopped

# Container keeps restarting
docker logs container_name  # Check error logs
docker-compose up --abort-on-container-exit  # Debug startup

# Cannot connect between services
docker network ls  # Check networks
docker network inspect network_name  # Verify service is on network
docker exec -it container_name nslookup service_name  # DNS resolution

# Volume permission issues
docker exec container_name ls -la /path/to/volume
docker exec container_name id  # Check user ID
# Fix: Set proper ownership in Dockerfile or use user mapping

# Out of disk space
docker system prune -a --volumes  # Clean everything
docker image prune -a  # Remove unused images
docker volume prune  # Remove unused volumes
```

## Best Practices

### Development Workflow

```yaml
# docker-compose.yml for development
version: '3.9'

services:
  app:
    build:
      context: .
      target: development
    volumes:
      # Mount source code
      - .:/app
      # Prevent node_modules from being overwritten
      - /app/node_modules
    environment:
      - NODE_ENV=development
    command: npm run dev
    ports:
      - "3000:3000"
      - "9229:9229"  # Node.js debugging port
```

### CI/CD Integration

```bash
#!/bin/bash
# ci-build.sh

# Build and tag images
docker-compose build --parallel

# Run tests
docker-compose -f docker-compose.test.yml run --rm tests

# Push images if tests pass
if [ $? -eq 0 ]; then
  docker-compose push
else
  echo "Tests failed, not pushing images"
  exit 1
fi

# Clean up
docker-compose down -v
```

### Production Deployment

```bash
#!/bin/bash
# deploy.sh

# Pull latest images
docker-compose -f docker-compose.yml -f docker-compose.prod.yml pull

# Start services with rolling update
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Wait for health checks
sleep 30

# Verify deployment
docker-compose ps
docker-compose exec -T app curl -f http://localhost:3000/health || exit 1

# Clean up old images
docker image prune -f
```

### Compose Tips

1. **Use specific image tags** - Never use `latest` in production
2. **Set resource limits** - Prevent container resource exhaustion
3. **Use health checks** - Ensure services are actually ready
4. **Externalize configuration** - Use environment variables and secrets
5. **Version your compose files** - Track infrastructure changes
6. **Use multi-stage builds** - Optimize image size
7. **Implement proper logging** - Centralize logs for production
8. **Regular cleanup** - Automate pruning of unused resources

### Security Guidelines

1. **Don't run as root** - Use USER directive in Dockerfile
2. **Scan images** - Use tools like Trivy or Snyk
3. **Use secrets management** - Never hardcode sensitive data
4. **Network isolation** - Use internal networks where possible
5. **Read-only containers** - Make filesystem read-only when possible
6. **Update regularly** - Keep base images and dependencies current
7. **Limit capabilities** - Drop unnecessary Linux capabilities
8. **Use official images** - Prefer official images from Docker Hub

## Advanced Patterns

### Blue-Green Deployment

```bash
# Blue-green deployment with Compose
# docker-compose.blue.yml and docker-compose.green.yml

# Deploy green while blue is running
docker-compose -f docker-compose.green.yml up -d

# Test green deployment
curl http://localhost:8081/health

# Switch traffic (update load balancer/proxy)
# Then stop blue
docker-compose -f docker-compose.blue.yml down
```

### Using Docker Compose with BuildKit

```bash
# Enable BuildKit for better builds
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1

# Use inline cache
docker-compose build --build-arg BUILDKIT_INLINE_CACHE=1

# Multi-platform builds
docker buildx create --use
docker buildx build --platform linux/amd64,linux/arm64 -t myapp:latest .
```

## Conclusion

Docker CLI and Docker Compose are essential tools for modern development workflows. Key takeaways:

1. **Master the basics** before moving to advanced features
2. **Use Compose** for multi-container applications
3. **Implement health checks** for production readiness
4. **Manage resources** to prevent system overload
5. **Secure containers** by default
6. **Automate workflows** for consistency
7. **Monitor and log** everything in production
8. **Keep learning** as Docker ecosystem evolves

The combination of Docker CLI and Compose provides a powerful platform for developing, testing, and deploying containerized applications efficiently.