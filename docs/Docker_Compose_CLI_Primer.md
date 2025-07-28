# Docker Compose and Docker CLI Primer

## Comprehensive Guide to Docker Command Line Tools

### Table of Contents
1. [Docker CLI Fundamentals](#docker-cli-fundamentals)
2. [Image Management](#image-management)
3. [Container Operations](#container-operations)
4. [Docker Compose Basics](#docker-compose-basics)
5. [Advanced Compose Features](#advanced-compose-features)
6. [Networking](#networking)
7. [Volumes and Storage](#volumes-and-storage)
8. [Environment Management](#environment-management)
9. [Debugging and Troubleshooting](#debugging-and-troubleshooting)
10. [Production Best Practices](#production-best-practices)

## Docker CLI Fundamentals

### Docker Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Docker CLI    │────▶│  Docker Daemon  │────▶│ Container       │
│   (Client)      │     │  (Server)       │     │ Runtime         │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                       │                         │
         │                       ▼                         ▼
         │              ┌─────────────────┐     ┌─────────────────┐
         │              │  Image Store    │     │  Containers     │
         │              └─────────────────┘     └─────────────────┘
         │                       │
         └───────────────────────┴──────── REST API
```

### Essential Docker Commands

```bash
# Docker version and info
docker version              # Client and server version
docker info                 # System-wide information
docker system df           # Disk usage
docker system prune        # Clean up unused resources

# Docker help
docker --help              # General help
docker <command> --help    # Command-specific help
docker run --help          # Detailed run options

# Context management (multiple Docker hosts)
docker context ls          # List contexts
docker context use remote  # Switch to remote Docker host
docker context create remote --docker "host=ssh://user@remotehost"
```

### Docker Command Structure

```bash
# Basic structure
docker [OPTIONS] COMMAND [ARG...]
docker [OPTIONS] MANAGEMENT_COMMAND SUBCOMMAND [ARG...]

# Management commands (organized by object type)
docker container ...       # Manage containers
docker image ...          # Manage images
docker network ...        # Manage networks
docker volume ...         # Manage volumes
docker system ...         # Manage Docker
docker plugin ...         # Manage plugins
docker secret ...         # Manage Docker secrets
docker service ...        # Manage services
docker stack ...          # Manage Docker stacks
docker swarm ...          # Manage Swarm
```

## Image Management

### Building Images

```bash
# Basic build
docker build -t myapp:latest .
docker build -t myapp:v1.0.0 -t myapp:latest .

# Advanced build options
docker build \
  --build-arg VERSION=1.0.0 \
  --build-arg BUILD_DATE=$(date -u +"%Y-%m-%dT%H:%M:%SZ") \
  --target production \
  --cache-from myapp:latest \
  --platform linux/amd64,linux/arm64 \
  -t myapp:latest .

# Build with custom Dockerfile
docker build -f Dockerfile.production -t myapp:prod .

# Build with BuildKit (improved performance)
DOCKER_BUILDKIT=1 docker build -t myapp:latest .

# Multi-stage build example
cat > Dockerfile << 'EOF'
# Build stage
FROM node:18-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
RUN npm run build

# Runtime stage
FROM node:18-alpine
WORKDIR /app
COPY --from=builder /app/dist ./dist
COPY --from=builder /app/node_modules ./node_modules
EXPOSE 3000
CMD ["node", "dist/index.js"]
EOF
```

### Managing Images

```bash
# List images
docker images                      # All images
docker images -a                   # Include intermediate images
docker images --filter "dangling=true"  # Untagged images
docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}"

# Image inspection
docker image inspect myapp:latest
docker image inspect myapp:latest --format='{{.Config.Env}}'
docker history myapp:latest        # Show image layers

# Tagging images
docker tag myapp:latest myregistry.com/myapp:latest
docker tag myapp:latest myapp:v1.0.0

# Removing images
docker rmi myapp:latest           # Remove image
docker rmi $(docker images -q -f dangling=true)  # Remove dangling images
docker image prune               # Remove unused images
docker image prune -a            # Remove all unused images

# Saving and loading images
docker save myapp:latest | gzip > myapp.tar.gz
docker load < myapp.tar.gz
docker save -o myapp.tar myapp:latest
docker load -i myapp.tar
```

### Registry Operations

```bash
# Login to registries
docker login                      # Docker Hub
docker login myregistry.com
echo $PASSWORD | docker login -u $USERNAME --password-stdin

# Push and pull
docker push myregistry.com/myapp:latest
docker pull myregistry.com/myapp:latest
docker pull myregistry.com/myapp@sha256:abc123...  # Pull by digest

# Search Docker Hub
docker search nginx
docker search --limit 5 --filter stars=100 nginx

# Registry management
docker manifest inspect nginx:latest
docker manifest create myapp:latest \
  myapp:latest-amd64 \
  myapp:latest-arm64
```

## Container Operations

### Running Containers

```bash
# Basic run
docker run nginx
docker run -d nginx               # Detached mode
docker run -it ubuntu bash        # Interactive with TTY
docker run --rm alpine echo hi    # Remove after exit

# Advanced run options
docker run -d \
  --name web \
  --hostname web.local \
  -p 8080:80 \
  -p 443:443 \
  -v /host/data:/container/data \
  -v myvolume:/data \
  -e ENV_VAR=value \
  -e SECRET_VAR \
  --env-file .env \
  --network mynetwork \
  --restart unless-stopped \
  --memory 512m \
  --cpus 0.5 \
  --health-cmd "curl -f http://localhost/health || exit 1" \
  --health-interval 30s \
  --health-timeout 3s \
  --health-retries 3 \
  --log-driver json-file \
  --log-opt max-size=10m \
  --log-opt max-file=3 \
  --user 1000:1000 \
  --read-only \
  --tmpfs /tmp \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  --cap-add NET_BIND_SERVICE \
  nginx:latest
```

### Container Management

```bash
# List containers
docker ps                         # Running containers
docker ps -a                      # All containers
docker ps -q                      # Only IDs
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Container lifecycle
docker start web
docker stop web
docker restart web
docker pause web
docker unpause web
docker kill web                   # Force stop
docker rm web                     # Remove container
docker rm -f web                  # Force remove

# Batch operations
docker stop $(docker ps -q)       # Stop all containers
docker rm $(docker ps -aq)        # Remove all containers
docker container prune            # Remove stopped containers

# Container inspection
docker inspect web
docker inspect web --format='{{.NetworkSettings.IPAddress}}'
docker port web                   # Show port mappings
docker top web                    # Show processes
docker stats                      # Live resource usage
docker stats --no-stream          # One-time snapshot
```

### Interacting with Containers

```bash
# Execute commands
docker exec web ls -la
docker exec -it web bash
docker exec -u root web apt-get update

# Copy files
docker cp file.txt web:/tmp/
docker cp web:/var/log/nginx/access.log ./
docker cp -a web:/etc/nginx ./nginx-config

# View logs
docker logs web
docker logs -f web                # Follow logs
docker logs --tail 50 web         # Last 50 lines
docker logs --since 10m web       # Last 10 minutes
docker logs -f --until 2023-12-01 web

# Attach to container
docker attach web                 # Attach to main process
# Detach: Ctrl-P Ctrl-Q (keep running)
# Exit: Ctrl-D or exit (stops container)

# Container changes
docker diff web                   # Show filesystem changes
docker commit web myapp:snapshot  # Create image from container
```

## Docker Compose Basics

### Docker Compose File Structure

```yaml
# docker-compose.yml
version: '3.9'  # Compose file version

services:       # Service definitions
  web:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./html:/usr/share/nginx/html
    networks:
      - frontend

  api:
    build: 
      context: ./api
      dockerfile: Dockerfile
    environment:
      - NODE_ENV=production
    networks:
      - frontend
      - backend

  db:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: secret
    volumes:
      - db-data:/var/lib/postgresql/data
    networks:
      - backend

networks:       # Network definitions
  frontend:
    driver: bridge
  backend:
    driver: bridge

volumes:        # Volume definitions
  db-data:
    driver: local
```

### Essential Compose Commands

```bash
# Basic commands
docker-compose up                 # Start services
docker-compose up -d              # Start in background
docker-compose down               # Stop and remove
docker-compose stop               # Stop services
docker-compose start              # Start services
docker-compose restart            # Restart services
docker-compose pause              # Pause services
docker-compose unpause            # Unpause services

# Build and rebuild
docker-compose build              # Build services
docker-compose build --no-cache   # Build without cache
docker-compose up --build         # Build and start
docker-compose build api          # Build specific service

# Service management
docker-compose ps                 # List services
docker-compose ps -a              # All services
docker-compose top                # Display running processes
docker-compose logs               # View logs
docker-compose logs -f api        # Follow specific service logs
docker-compose logs --tail=50     # Last 50 lines

# Scaling services
docker-compose up --scale api=3   # Run 3 instances of api
docker-compose scale api=5        # Scale to 5 instances

# Execute commands
docker-compose exec api bash      # Execute in running container
docker-compose exec -T db pg_dump # Execute without TTY
docker-compose run api npm test   # Run one-off command
docker-compose run --rm api bash  # Run and remove after
```

### Compose Configuration

```yaml
# Extended docker-compose.yml with all features
version: '3.9'

services:
  web:
    image: nginx:alpine
    container_name: puzzle_web      # Custom container name
    hostname: web.local            # Container hostname
    
    # Build configuration
    build:
      context: ./web              # Build context
      dockerfile: Dockerfile.prod  # Custom Dockerfile
      args:                       # Build arguments
        - VERSION=1.0.0
        - BUILD_DATE=${BUILD_DATE}
      cache_from:
        - nginx:alpine
      target: production          # Multi-stage target
    
    # Networking
    ports:
      - "80:80"                   # Host:Container
      - "443:443"
      - "127.0.0.1:8080:8080"    # Bind to localhost only
    expose:
      - "9090"                    # Internal port only
    networks:
      frontend:
        aliases:
          - web.local
          - nginx.local
      backend:
        ipv4_address: 172.20.0.5
    extra_hosts:
      - "host.docker.internal:host-gateway"
    
    # Storage
    volumes:
      - ./html:/usr/share/nginx/html:ro  # Read-only
      - ./nginx.conf:/etc/nginx/nginx.conf
      - web-logs:/var/log/nginx
      - type: tmpfs
        target: /tmp
        tmpfs:
          size: 100M
    
    # Environment
    environment:
      - ENV=production
      - DEBUG=false
      - API_URL=http://api:3000
    env_file:
      - .env
      - .env.production
    
    # Resource constraints
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
    
    # Health check
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 40s
    
    # Dependencies and startup
    depends_on:
      api:
        condition: service_healthy
      db:
        condition: service_started
    restart: unless-stopped
    
    # Logging
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
    
    # Security
    security_opt:
      - no-new-privileges:true
    cap_drop:
      - ALL
    cap_add:
      - NET_BIND_SERVICE
    user: "1000:1000"
    read_only: true
    
    # Other options
    stdin_open: true              # -i
    tty: true                     # -t
    privileged: false
    working_dir: /app
    entrypoint: /entrypoint.sh
    command: ["nginx", "-g", "daemon off;"]
```

## Advanced Compose Features

### Multiple Compose Files

```bash
# Override configurations
docker-compose -f docker-compose.yml -f docker-compose.override.yml up

# Environment-specific configs
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up

# Structure example
tree
.
├── docker-compose.yml          # Base configuration
├── docker-compose.override.yml # Development overrides (default)
├── docker-compose.prod.yml     # Production overrides
├── docker-compose.test.yml     # Test environment
└── docker-compose.ci.yml       # CI/CD specific
```

```yaml
# docker-compose.yml (base)
version: '3.9'
services:
  api:
    image: myapp:latest
    environment:
      - NODE_ENV=production

# docker-compose.override.yml (dev)
version: '3.9'
services:
  api:
    build: .
    volumes:
      - ./src:/app/src
    environment:
      - NODE_ENV=development
      - DEBUG=true
    ports:
      - "3000:3000"

# docker-compose.prod.yml
version: '3.9'
services:
  api:
    deploy:
      replicas: 3
    environment:
      - NODE_ENV=production
      - LOG_LEVEL=warn
```

### Extends and Anchors

```yaml
# Using YAML anchors for reusability
version: '3.9'

x-common-variables: &common-variables
  REDIS_HOST: redis
  REDIS_PORT: 6379
  LOG_LEVEL: info

x-app-defaults: &app-defaults
  restart: unless-stopped
  networks:
    - backend
  logging:
    driver: json-file
    options:
      max-size: "10m"
      max-file: "3"

services:
  api:
    <<: *app-defaults
    image: myapp:api
    environment:
      <<: *common-variables
      SERVICE_NAME: api
      PORT: 3000

  worker:
    <<: *app-defaults
    image: myapp:worker
    environment:
      <<: *common-variables
      SERVICE_NAME: worker
      CONCURRENCY: 10

  redis:
    image: redis:alpine
    <<: *app-defaults
```

### Advanced Networking

```yaml
version: '3.9'

networks:
  frontend:
    driver: bridge
    driver_opts:
      com.docker.network.bridge.name: br-frontend
    ipam:
      driver: default
      config:
        - subnet: 172.20.0.0/24
          gateway: 172.20.0.1

  backend:
    driver: bridge
    internal: true  # No external access
    ipam:
      config:
        - subnet: 172.21.0.0/24

  external_network:
    external: true
    name: existing-network

services:
  web:
    networks:
      - frontend
      - external_network
    
  api:
    networks:
      frontend:
        ipv4_address: 172.20.0.10
      backend:
        aliases:
          - api.backend
          - api-service
    
  db:
    networks:
      - backend
```

### Secrets Management

```yaml
version: '3.9'

secrets:
  db_password:
    file: ./secrets/db_password.txt
  api_key:
    external: true
    external_name: prod_api_key

services:
  db:
    image: postgres:15
    secrets:
      - db_password
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
  
  api:
    image: myapp:latest
    secrets:
      - source: api_key
        target: /app/config/api_key
        uid: '1000'
        gid: '1000'
        mode: 0400
```

## Networking

### Network Types and Usage

```bash
# List networks
docker network ls
docker network ls --filter driver=bridge

# Inspect network
docker network inspect bridge
docker network inspect bridge --format='{{.Containers}}'

# Create networks
docker network create mynet
docker network create --driver bridge mybridge
docker network create --driver overlay myoverlay
docker network create \
  --driver bridge \
  --subnet 172.28.0.0/16 \
  --ip-range 172.28.5.0/24 \
  --gateway 172.28.0.1 \
  --attachable \
  --opt com.docker.network.bridge.name=docker1 \
  custom

# Connect/disconnect containers
docker network connect mynet web
docker network connect --ip 172.28.0.10 mynet web
docker network disconnect mynet web

# Remove networks
docker network rm mynet
docker network prune  # Remove unused networks
```

### Network Troubleshooting

```bash
# Test connectivity between containers
docker run --rm --network mynet alpine ping api
docker run --rm --network mynet nicolaka/netshoot \
  nslookup api

# Inspect container networking
docker exec web ip addr
docker exec web netstat -tulpn
docker exec web ss -tulpn
docker exec web cat /etc/hosts
docker exec web cat /etc/resolv.conf

# Port mapping verification
docker port web
iptables -t nat -L -n | grep 8080
```

## Volumes and Storage

### Volume Management

```bash
# List volumes
docker volume ls
docker volume ls --filter dangling=true

# Create volumes
docker volume create mydata
docker volume create --driver local \
  --opt type=tmpfs \
  --opt device=tmpfs \
  --opt o=size=100m,uid=1000 \
  tmpdata

# Inspect volumes
docker volume inspect mydata
docker volume inspect mydata --format='{{.Mountpoint}}'

# Remove volumes
docker volume rm mydata
docker volume prune  # Remove unused volumes
docker volume prune --filter "label!=keep"

# Backup and restore
# Backup
docker run --rm \
  -v mydata:/source:ro \
  -v $(pwd):/backup \
  alpine tar czf /backup/mydata.tar.gz -C /source .

# Restore
docker run --rm \
  -v mydata:/target \
  -v $(pwd):/backup:ro \
  alpine tar xzf /backup/mydata.tar.gz -C /target
```

### Bind Mounts vs Volumes

```bash
# Bind mount (host path)
docker run -v /host/path:/container/path myapp
docker run -v $(pwd)/data:/app/data myapp
docker run -v /host/path:/container/path:ro myapp  # Read-only

# Named volume
docker run -v mydata:/app/data myapp
docker run -v mydata:/app/data:ro myapp

# Anonymous volume
docker run -v /app/data myapp

# Mount options
docker run \
  --mount type=bind,source=/host/path,target=/app/data,readonly \
  --mount type=volume,source=mydata,target=/app/cache \
  --mount type=tmpfs,target=/app/tmp,tmpfs-size=100M \
  myapp
```

## Environment Management

### Environment Variables

```bash
# Pass environment variables
docker run -e VAR=value myapp
docker run -e VAR myapp  # Pass from host
docker run --env-file .env myapp

# .env file format
DATABASE_URL=postgres://user:pass@db:5432/mydb
REDIS_URL=redis://redis:6379
API_KEY=secret123
# Comments are supported
EMPTY_VAR=

# Docker Compose environment precedence
# 1. Compose file `environment` section
# 2. Shell environment variables
# 3. Environment file
# 4. Dockerfile ENV
# 5. Variable is not defined

# Variable substitution in Compose
version: '3.9'
services:
  api:
    image: myapp:${VERSION:-latest}
    environment:
      - DATABASE_URL=${DATABASE_URL}
      - API_PORT=${API_PORT:-3000}
      - DEBUG=${DEBUG:-false}
```

### Configuration Management

```yaml
# Using configs in Docker Compose
version: '3.9'

configs:
  nginx_config:
    file: ./nginx.conf
  app_config:
    external: true
    external_name: prod_app_config

services:
  web:
    image: nginx
    configs:
      - source: nginx_config
        target: /etc/nginx/nginx.conf
        mode: 0644
  
  api:
    image: myapp
    configs:
      - source: app_config
        target: /app/config.json
        uid: '1000'
        gid: '1000'
        mode: 0400
```

## Debugging and Troubleshooting

### Container Debugging

```bash
# Debug failed containers
docker ps -a  # See exited containers
docker logs <container>  # Check logs
docker inspect <container> --format='{{.State.ExitCode}}'
docker inspect <container> --format='{{.State.Error}}'

# Debug running containers
docker exec -it <container> /bin/sh
docker exec -it <container> /bin/bash
docker exec -u root <container> apt-get install -y curl

# Resource issues
docker stats
docker system df
docker system events  # Real-time events
docker system prune -a  # Clean everything

# Debug builds
docker build --no-cache -t test .
docker build --progress=plain -t test .  # Verbose output
DOCKER_BUILDKIT=0 docker build -t test .  # Disable BuildKit

# Debug networking
docker run --rm --net container:<container> nicolaka/netshoot
docker run --rm -it --pid container:<container> --cap-add SYS_PTRACE alpine
```

### Common Issues and Solutions

```bash
# Permission denied
# Solution 1: Add user to docker group
sudo usermod -aG docker $USER
newgrp docker

# Solution 2: Fix socket permissions
sudo chmod 666 /var/run/docker.sock

# Cannot connect to Docker daemon
sudo systemctl status docker
sudo systemctl start docker
sudo dockerd  # Run in foreground for debugging

# Disk space issues
docker system prune -a --volumes
docker builder prune
df -h /var/lib/docker

# Container keeps restarting
docker logs <container> --tail 50
docker events --filter container=<container>
docker update --restart=no <container>

# Slow builds
# Use BuildKit
export DOCKER_BUILDKIT=1
# Use cache mounts
# syntax=docker/dockerfile:1
FROM node:18
RUN --mount=type=cache,target=/root/.npm \
    npm install

# Port already in use
lsof -i :8080
docker ps --filter "publish=8080"
```

## Production Best Practices

### Security Best Practices

```dockerfile
# Secure Dockerfile
FROM node:18-alpine AS builder
# Run as non-root during build
RUN addgroup -g 1001 -S nodejs && adduser -S nodejs -u 1001
USER nodejs
WORKDIR /app
COPY --chown=nodejs:nodejs package*.json ./
RUN npm ci --only=production
COPY --chown=nodejs:nodejs . .

FROM node:18-alpine
# Install security updates
RUN apk update && apk upgrade && apk add --no-cache dumb-init
# Create non-root user
RUN addgroup -g 1001 -S nodejs && adduser -S nodejs -u 1001
USER nodejs
WORKDIR /app
COPY --from=builder --chown=nodejs:nodejs /app .
EXPOSE 3000
ENTRYPOINT ["dumb-init", "--"]
CMD ["node", "index.js"]
```

### Production Compose Configuration

```yaml
# docker-compose.prod.yml
version: '3.9'

services:
  web:
    image: myregistry.com/myapp:${VERSION}
    deploy:
      mode: replicated
      replicas: 3
      restart_policy:
        condition: any
        delay: 5s
        max_attempts: 3
        window: 120s
      update_config:
        parallelism: 1
        delay: 10s
        failure_action: rollback
        monitor: 60s
        max_failure_ratio: 0.3
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
    healthcheck:
      test: ["CMD", "wget", "--quiet", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "5"
        labels: "service=web,environment=production"
    security_opt:
      - no-new-privileges:true
    read_only: true
    tmpfs:
      - /tmp
      - /var/run
```

### Deployment Patterns

```bash
# Blue-Green Deployment
# Start green environment
docker-compose -p green -f docker-compose.yml up -d
# Test green environment
curl http://localhost:8081/health
# Switch traffic (update load balancer)
# Stop blue environment
docker-compose -p blue down

# Rolling Update
docker-compose up -d --no-deps --scale api=6 api
# Gradually replace instances
docker-compose up -d --no-deps --force-recreate api

# Canary Deployment
# Run both versions
docker-compose up -d --scale api_v1=9 --scale api_v2=1
# Monitor metrics
# Gradually increase v2 instances
```

### Monitoring and Logging

```yaml
# Logging configuration
version: '3.9'

x-logging: &default-logging
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
    labels: "env,service,version"

services:
  api:
    image: myapp:latest
    labels:
      - "env=production"
      - "service=api"
      - "version=${VERSION}"
    logging: *default-logging
    
  # Centralized logging
  fluentd:
    image: fluent/fluentd:latest
    volumes:
      - ./fluentd.conf:/fluentd/etc/fluent.conf
      - /var/lib/docker/containers:/var/lib/docker/containers:ro
    ports:
      - "24224:24224"
```

### Backup and Disaster Recovery

```bash
#!/bin/bash
# backup.sh - Complete Docker environment backup

# Backup timestamp
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="./backups/${TIMESTAMP}"
mkdir -p "${BACKUP_DIR}"

# Stop services
docker-compose stop

# Backup volumes
for volume in $(docker volume ls -q); do
  echo "Backing up volume: ${volume}"
  docker run --rm \
    -v ${volume}:/source:ro \
    -v ${BACKUP_DIR}:/backup \
    alpine tar czf /backup/${volume}.tar.gz -C /source .
done

# Backup compose files and configs
cp docker-compose*.yml ${BACKUP_DIR}/
cp -r configs ${BACKUP_DIR}/

# Export running container states
docker ps -a --format "table {{.Names}}\t{{.Image}}\t{{.Status}}" > ${BACKUP_DIR}/containers.txt

# Backup images
docker save $(docker images -q) | gzip > ${BACKUP_DIR}/images.tar.gz

# Start services
docker-compose start

echo "Backup completed: ${BACKUP_DIR}"
```

## Quick Reference

### Essential Commands Cheatsheet

```bash
# Container Lifecycle
docker run -d --name app myimage    # Run detached
docker stop app                      # Stop gracefully
docker start app                     # Start stopped
docker restart app                   # Restart
docker rm -f app                     # Force remove

# Debugging
docker logs -f app                   # Follow logs
docker exec -it app sh              # Shell access
docker inspect app                   # Full details
docker stats app                     # Resource usage

# Compose Essentials
docker-compose up -d                 # Start services
docker-compose down -v               # Stop and clean
docker-compose logs -f               # Follow all logs
docker-compose ps                    # List services
docker-compose exec api sh           # Shell in service

# Cleanup
docker system prune -a               # Clean everything
docker container prune               # Remove stopped
docker image prune -a                # Remove unused images
docker volume prune                  # Remove unused volumes
docker network prune                 # Remove unused networks

# Quick Troubleshooting
docker version                       # Check installation
docker system df                     # Check disk usage
docker system events                 # Watch events
docker-compose config               # Validate compose file
```

### Common Patterns

```bash
# One-liner patterns
# Remove all containers
docker rm -f $(docker ps -aq)

# Remove dangling images
docker rmi $(docker images -f "dangling=true" -q)

# Copy files between containers
docker cp source_container:/path/file.txt - | docker cp - target_container:/path/

# Export/Import containers
docker export mycontainer | gzip > mycontainer.tar.gz
gunzip -c mycontainer.tar.gz | docker import - myimage:latest

# Follow logs of all compose services
docker-compose logs -f --tail=10

# Rebuild single service
docker-compose up -d --no-deps --build api

# Execute in all running containers
for c in $(docker ps -q); do docker exec $c date; done
```