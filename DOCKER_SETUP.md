# Docker Setup Guide - Tiered Approach

This guide provides a tiered approach to running the Collaborative Puzzle Platform in Docker, allowing you to test each layer incrementally.

## 📋 Prerequisites

- Docker Desktop installed and running
- Docker Compose v2.0+
- At least 4GB of available RAM for Docker
- Ports available: 1433, 1883, 3478, 5000, 6379, 9001

## 🎯 Tiered Testing Approach

We provide 4 tiers of Docker Compose configurations to help identify and resolve issues incrementally:

### Tier 1: Redis Only
**File:** `docker-compose.tier1-redis.yml`
- Tests basic Docker networking
- Verifies Redis connectivity
- Minimal resource usage

```bash
docker-compose -f docker-compose.tier1-redis.yml up
# Test: redis-cli -h localhost -p 6379 ping
```

### Tier 2: Infrastructure Services
**File:** `docker-compose.tier2-infra.yml`
- Redis + SQL Server + MQTT
- Tests all infrastructure dependencies
- No application code

```bash
docker-compose -f docker-compose.tier2-infra.yml up
# Test SQL: sqlcmd -S localhost,1433 -U sa -P "YourStrong@Passw0rd" -Q "SELECT 1"
# Test MQTT: mosquitto_sub -h localhost -p 1883 -t test
```

### Tier 3: API with Basic Infrastructure
**File:** `docker-compose.tier3-api.yml`
- Redis + API (in-memory database mode)
- Tests application startup and basic functionality
- Validates SignalR connectivity

```bash
# Build API image first
docker build -f Dockerfile.minimal -t puzzle-api:latest .

# Run tier 3
docker-compose -f docker-compose.tier3-api.yml up

# Test endpoints:
# http://localhost:5000/health
# http://localhost:5000/test.html
```

### Tier 4: Full Stack
**File:** `docker-compose.tier4-full.yml`
- All services: Redis, SQL Server, MQTT, TURN, API
- Full functionality enabled
- Production-like environment

```bash
docker-compose -f docker-compose.tier4-full.yml up
```

## 🚀 Quick Start

### Automated Testing
Run the automated test script to validate each tier:

```bash
./scripts/test-docker-tiers.sh
```

This script will:
1. Test each tier sequentially
2. Validate service health
3. Report any issues
4. Clean up between tiers

### Manual Testing

#### Start Tier 1 (Redis):
```bash
docker-compose -f docker-compose.tier1-redis.yml up -d
docker exec puzzle-redis redis-cli ping
# Expected: PONG
docker-compose -f docker-compose.tier1-redis.yml down
```

#### Start Tier 2 (Infrastructure):
```bash
docker-compose -f docker-compose.tier2-infra.yml up -d
# Wait for services to be healthy
docker-compose -f docker-compose.tier2-infra.yml ps
# All should show "healthy"
docker-compose -f docker-compose.tier2-infra.yml down
```

#### Start Tier 3 (API Basic):
```bash
# Build API image
docker build -f Dockerfile.minimal -t puzzle-api:latest .

# Start services
docker-compose -f docker-compose.tier3-api.yml up -d

# Test health
curl http://localhost:5000/health

# View logs if issues
docker-compose -f docker-compose.tier3-api.yml logs api

# Stop
docker-compose -f docker-compose.tier3-api.yml down
```

#### Start Tier 4 (Full Stack):
```bash
docker-compose -f docker-compose.tier4-full.yml up -d

# Test all endpoints
curl http://localhost:5000/health
# Open in browser:
# - http://localhost:5000/test.html (SignalR)
# - http://localhost:5000/websocket-test.html (WebSocket)
# - http://localhost:5000/webrtc-test.html (WebRTC)
# - http://localhost:5000/mqtt-dashboard.html (MQTT)
# - http://localhost:5000/socketio-test.html (Socket.IO)
```

## 🔍 Troubleshooting

### Common Issues

#### Port Already in Use
```bash
# Find process using port (example for 5000)
lsof -i :5000  # Mac/Linux
netstat -ano | findstr :5000  # Windows

# Kill process or change port in docker-compose
```

#### SQL Server Won't Start
- Ensure you have at least 2GB RAM allocated to Docker
- Check logs: `docker-compose logs sqlserver`
- Try increasing start_period in healthcheck

#### API Can't Connect to Services
- Check service names match in connection strings
- Verify services are on same network
- Check logs: `docker-compose logs api`

#### Build Failures
- Clear Docker cache: `docker system prune -a`
- Rebuild without cache: `docker build --no-cache -f Dockerfile.minimal -t puzzle-api:latest .`

### Viewing Logs
```bash
# All services
docker-compose -f docker-compose.tier[N]-[name].yml logs

# Specific service
docker-compose -f docker-compose.tier[N]-[name].yml logs [service]

# Follow logs
docker-compose -f docker-compose.tier[N]-[name].yml logs -f
```

### Service Health
```bash
# Check service status
docker-compose -f docker-compose.tier[N]-[name].yml ps

# Inspect health
docker inspect puzzle-redis | grep -A 5 Health
```

## 📊 Resource Usage

Approximate resource requirements:

| Tier | Services | RAM | CPU | Disk |
|------|----------|-----|-----|------|
| 1 | Redis | 100MB | Low | 50MB |
| 2 | +SQL, MQTT | 2GB | Medium | 500MB |
| 3 | +API | 2.5GB | Medium | 1GB |
| 4 | +TURN | 3GB | High | 1.5GB |

## 🛠️ Development Workflow

1. **Start with Tier 3** for most development:
   ```bash
   docker-compose -f docker-compose.tier3-api.yml up
   ```

2. **Use Tier 4** when testing:
   - WebRTC features
   - MQTT integration
   - Full database operations

3. **Rebuild API** after code changes:
   ```bash
   docker-compose -f docker-compose.tier3-api.yml build api
   docker-compose -f docker-compose.tier3-api.yml up -d
   ```

## 🔐 Security Notes

- Default passwords are for development only
- Change all passwords before deploying
- Use secrets management in production
- Enable TLS/SSL for all services

## 📚 Next Steps

1. Run tier testing: `./scripts/test-docker-tiers.sh`
2. Choose appropriate tier for your needs
3. Access test pages to verify functionality
4. Check logs if issues arise
5. Move to next tier when ready

For production deployment, see [PRODUCTION_DEPLOYMENT.md](docs/PRODUCTION_DEPLOYMENT.md)