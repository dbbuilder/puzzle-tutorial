# Development Guide

## Quick Start

### Prerequisites
- Docker Desktop
- .NET 9 SDK (for local development)
- Visual Studio 2022 or VS Code

### Running Locally

1. **Start Dependencies**
```bash
# Start Redis only
docker run -d --name redis -p 6379:6379 redis:7-alpine

# Or use docker-compose for all services
docker-compose up -d
```

2. **Run the API**
```bash
cd src/CollaborativePuzzle.Api
dotnet run
```

3. **Test the API**
- Swagger UI: http://localhost:5000/swagger
- Health Check: http://localhost:5000/api/test/health
- SignalR Test: http://localhost:5000/test.html

### Docker Development

#### Build and Run
```bash
# Quick build
./scripts/build-minimal.sh

# Full docker-compose
docker-compose up --build
```

#### Troubleshooting Docker
```bash
# Check logs
docker logs puzzle-api

# Enter container
docker exec -it puzzle-api bash

# Clean up
docker-compose down -v
docker system prune -a
```

## Testing

### Unit Tests
```bash
dotnet test --filter "Category!=Integration"
```

### Integration Tests
```bash
# Start dependencies first
docker-compose up -d redis sqlserver

# Run tests
dotnet test --filter "Category=Integration"
```

### SignalR Testing
1. Open http://localhost:5000/test.html
2. Click "Connect"
3. Join a session with any ID
4. Test various hub methods

### Load Testing
```bash
# Using NBomber (install first)
dotnet tool install -g NBomber.Http

# Run load test
nbomber-http --url http://localhost:5000/api/test/health --rate 100 --duration 60s
```

## Architecture

### Project Structure
```
src/
├── CollaborativePuzzle.Api/          # Web API and SignalR hubs
├── CollaborativePuzzle.Core/         # Domain entities and interfaces
├── CollaborativePuzzle.Infrastructure/ # Data access and external services
└── CollaborativePuzzle.Hubs/         # SignalR hub implementations

tests/
├── CollaborativePuzzle.Tests/        # Unit tests
└── CollaborativePuzzle.IntegrationTests/ # Integration tests
```

### Key Technologies
- **SignalR**: Real-time communication
- **Redis**: Caching and SignalR backplane
- **SQL Server**: Primary data store
- **Docker**: Containerization
- **xUnit**: Testing framework

## Common Tasks

### Adding a New Hub Method

1. Add method to `PuzzleHub.cs`:
```csharp
public async Task<HubResult<MyResult>> MyNewMethod(string param)
{
    // Implementation
}
```

2. Add test in `PuzzleHubTests.cs`:
```csharp
[Fact]
public async Task MyNewMethod_Should_Work()
{
    // Test implementation
}
```

3. Update client in `test.html`:
```javascript
const result = await connection.invoke("MyNewMethod", param);
```

### Adding a New API Endpoint

1. Create controller or add to existing:
```csharp
[HttpGet("my-endpoint")]
public async Task<IActionResult> MyEndpoint()
{
    return Ok(new { data = "value" });
}
```

2. Test with Swagger or curl:
```bash
curl http://localhost:5000/api/controller/my-endpoint
```

### Debugging

#### VS Code
1. Install C# extension
2. Use provided launch.json
3. Press F5 to debug

#### Visual Studio
1. Open CollaborativePuzzle.sln
2. Set CollaborativePuzzle.Api as startup project
3. Press F5 to debug

#### Docker Debugging
```bash
# View logs
docker logs -f puzzle-api

# Check environment
docker exec puzzle-api printenv

# Test connectivity
docker exec puzzle-api curl http://localhost:8080/health
```

## Performance

### Monitoring
- Application Insights (when configured)
- Health checks: /health
- SignalR diagnostics in browser console

### Optimization Tips
1. Use MessagePack for SignalR (already configured)
2. Enable response compression
3. Use Redis for session state
4. Implement output caching for static data

## Deployment

### Local Docker
```bash
docker build -t puzzle-api .
docker run -p 8080:8080 puzzle-api
```

### Azure Container Registry
```bash
az acr build --registry myregistry --image puzzle-api .
```

### Kubernetes
```bash
kubectl apply -f k8s/
```

## Troubleshooting

### Common Issues

1. **Redis Connection Failed**
   - Check Redis is running: `docker ps`
   - Verify connection string in appsettings
   - Check firewall/network settings

2. **SignalR Not Connecting**
   - Check CORS settings
   - Verify WebSocket support
   - Check browser console for errors

3. **Docker Build Fails**
   - Clear Docker cache: `docker system prune -a`
   - Check disk space
   - Verify .dockerignore settings

4. **Tests Failing**
   - Ensure dependencies are running
   - Check test database migrations
   - Review test output for specifics

### Logging

Enable detailed logging in appsettings:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

## Resources

- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr)
- [Redis Documentation](https://redis.io/documentation)
- [Docker Documentation](https://docs.docker.com)
- [xUnit Documentation](https://xunit.net)

## Contributing

1. Create feature branch
2. Write tests first (TDD)
3. Implement feature
4. Ensure all tests pass
5. Update documentation
6. Submit pull request