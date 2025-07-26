# Tiered Docker Implementation Memory

## Key Learning: Docker Compose Version Attribute

**Warning Encountered:**
```
the attribute `version` is obsolete, it will be ignored, please remove it to avoid potential confusion
```

**Resolution:** Modern Docker Compose (v2+) no longer requires the `version` attribute. Remove `version: '3.8'` from all compose files.

## Tiered Docker Approach Success

### What Works Well

1. **Incremental Testing Strategy**
   - Tier 1 (Redis): ✅ Verified - Redis starts and responds to ping
   - Allows identifying issues at each layer
   - Reduces debugging complexity

2. **Benefits Discovered**
   - Faster iteration when testing specific components
   - Lower resource usage during development
   - Clear isolation of service dependencies
   - Easy rollback to working configuration

### Implementation Pattern

```yaml
# Modern Docker Compose (no version needed)
services:
  redis:
    image: redis:7-alpine
    container_name: puzzle-redis
    # ... rest of config
```

### Recommended Workflow

1. **Development**: Use Tier 3 (API + Redis)
   - Fastest startup
   - In-memory database mode
   - Sufficient for most feature development

2. **Integration Testing**: Use Tier 4 (Full Stack)
   - All services running
   - Real database connections
   - Complete feature validation

3. **Debugging**: Start with Tier 1, work up
   - Isolate problematic services
   - Validate each layer independently

### Key Commands

```bash
# Quick test any tier
docker-compose -f docker-compose.tier[N]-[name].yml up -d
docker-compose -f docker-compose.tier[N]-[name].yml ps
docker-compose -f docker-compose.tier[N]-[name].yml logs [service]
docker-compose -f docker-compose.tier[N]-[name].yml down

# Test connectivity
docker exec puzzle-redis redis-cli ping
docker exec puzzle-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -Q "SELECT 1"
curl http://localhost:5000/health
```

### Network Isolation

Each tier uses the same network name (`puzzle-network`) allowing:
- Services to find each other by container name
- Easy migration between tiers
- Consistent configuration across environments

### Health Checks Are Critical

Proper health checks prevent cascade failures:
- Redis: `redis-cli ping`
- SQL Server: `sqlcmd SELECT 1`
- API: `curl /health`
- Use `condition: service_healthy` for dependencies

### Volume Management

Named volumes persist data between tier changes:
- `redis-data`
- `sqlserver-data`
- `mqtt-data`

This allows switching tiers without data loss.

## Lessons for Future Projects

1. **Always implement tiered Docker Compose** for complex applications
2. **Remove version attribute** from modern Docker Compose files
3. **Start simple** - single service first, then build up
4. **Health checks are mandatory** - not optional
5. **Document each tier's purpose** clearly
6. **Provide test commands** for each service

## For CLAUDE.md Global Instructions

Add to global CLAUDE.md:

```markdown
### Docker Compose Best Practices

When creating Docker Compose configurations:

1. **Use Tiered Approach**: Create multiple compose files for incremental testing
   - tier1: Core infrastructure (Redis, DB)
   - tier2: Add supporting services (MQTT, etc.)
   - tier3: Add application with minimal config
   - tier4: Full production-like setup

2. **Modern Compose Format**: Don't use `version` attribute (deprecated in Compose v2+)

3. **Always Include**:
   - Health checks for every service
   - Named networks for service communication
   - Named volumes for data persistence
   - Clear container names for easy debugging

4. **Testing Pattern**:
   ```bash
   # Start services
   docker-compose -f docker-compose.tierN.yml up -d
   
   # Verify health
   docker-compose -f docker-compose.tierN.yml ps
   
   # Test connectivity
   docker exec [container] [test-command]
   
   # Check logs if issues
   docker-compose -f docker-compose.tierN.yml logs [service]
   
   # Clean up
   docker-compose -f docker-compose.tierN.yml down
   ```

5. **Benefits**: Faster debugging, resource efficiency, isolated testing, progressive validation
```

This tiered approach has proven highly effective for complex applications with multiple services.