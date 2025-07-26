# Collaborative Puzzle Platform - Testing Guide

## Overview

This guide covers the comprehensive testing infrastructure implemented for the Collaborative Puzzle Platform, following Test-Driven Development (TDD) principles.

## Testing Layers

### 1. Unit Tests (`CollaborativePuzzle.Tests`)
- **Purpose**: Test individual components in isolation
- **Framework**: xUnit, Moq, FluentAssertions
- **Coverage Goal**: 90%+

### 2. Integration Tests (`CollaborativePuzzle.IntegrationTests`)
- **Purpose**: Test component interactions with real dependencies
- **Framework**: xUnit, Testcontainers, WebApplicationFactory
- **Features**:
  - Automated Docker containers for SQL Server and Redis
  - Real SignalR hub testing
  - API endpoint validation

### 3. End-to-End Tests (`CollaborativePuzzle.E2ETests`)
- **Purpose**: Test complete user scenarios through the browser
- **Framework**: Playwright
- **Scenarios**:
  - SignalR connection and messaging
  - Multi-user puzzle collaboration
  - WebRTC voice chat
  - Error handling and reconnection

### 4. Performance Tests (`CollaborativePuzzle.PerformanceTests`)
- **Purpose**: Measure and benchmark performance
- **Framework**: BenchmarkDotNet
- **Benchmarks**:
  - Redis operations (get, set, locking, pub/sub)
  - SignalR message latency
  - Concurrent connection handling
  - Serialization performance

### 5. Load Tests (`CollaborativePuzzle.LoadTests`)
- **Purpose**: Stress test the system under load
- **Framework**: NBomber
- **Scenarios**:
  - Concurrent user sessions
  - Message broadcasting at scale
  - API endpoint stress testing
  - Connection pool exhaustion

## Running Tests

### Quick Start
```bash
# Run all tests
./scripts/run-tests.sh

# Run specific test suite
dotnet test tests/CollaborativePuzzle.Tests
dotnet test tests/CollaborativePuzzle.IntegrationTests
```

### Using the Test Runner Script
The `run-tests.sh` script provides an interactive menu:

1. **Unit Tests** - No prerequisites
2. **Integration Tests** - Requires Docker
3. **E2E Tests** - Requires running application
4. **Performance Benchmarks** - Standalone benchmarks
5. **Load Tests** - Requires running application
6. **All Tests** - Runs complete test suite
7. **Code Coverage** - Generates HTML coverage report

### Docker Requirements for Integration Tests

Integration tests use Testcontainers to automatically manage dependencies:

```csharp
// Automatic container management
private readonly MsSqlContainer _sqlContainer;
private readonly RedisContainer _redisContainer;

public async Task InitializeAsync()
{
    await Task.WhenAll(
        _sqlContainer.StartAsync(),
        _redisContainer.StartAsync()
    );
}
```

## Test Patterns

### Unit Test Pattern
```csharp
[Fact]
public async Task MovePiece_WithValidMove_ShouldUpdatePosition()
{
    // Arrange
    var mockRepository = new Mock<IPieceRepository>();
    var service = new PuzzleService(mockRepository.Object);
    
    // Act
    var result = await service.MovePieceAsync("piece-1", new Position(100, 200));
    
    // Assert
    result.Should().BeTrue();
    mockRepository.Verify(r => r.UpdatePositionAsync(It.IsAny<string>(), It.IsAny<Position>()), Times.Once);
}
```

### Integration Test Pattern
```csharp
public class PuzzleApiTests : IntegrationTestBase
{
    [Fact]
    public async Task CreatePuzzle_WithValidData_ReturnsSuccess()
    {
        // Uses real database and Redis from Testcontainers
        var response = await Client.PostAsJsonAsync("/api/puzzles", new CreatePuzzleRequest
        {
            Name = "Test Puzzle",
            PieceCount = 100
        });
        
        response.Should().BeSuccessful();
    }
}
```

### E2E Test Pattern
```csharp
[Fact]
public async Task Should_Complete_Puzzle_Collaboration_Flow()
{
    // Navigate to application
    await Page.GotoAsync($"{BaseUrl}/puzzle/123");
    
    // Join session
    await Page.FillAsync("#username", "TestUser");
    await Page.ClickAsync("#joinButton");
    
    // Move piece
    await Page.DragAndDropAsync("#piece-1", "#target-position");
    
    // Verify piece moved
    await Page.WaitForSelectorAsync("text=Piece placed correctly");
}
```

### Performance Test Pattern
```csharp
[Benchmark]
[Arguments(10, 100, 1000)]
public async Task BatchRedisOperations(int batchSize)
{
    var tasks = new List<Task>();
    for (int i = 0; i < batchSize; i++)
    {
        tasks.Add(_redis.StringSetAsync($"key-{i}", $"value-{i}"));
    }
    await Task.WhenAll(tasks);
}
```

### Load Test Pattern
```csharp
Scenario.Create("concurrent_users", async context =>
{
    var connection = new HubConnectionBuilder()
        .WithUrl($"{BaseUrl}/puzzlehub")
        .Build();
        
    await connection.StartAsync();
    await connection.InvokeAsync("JoinSession", "load-test-session");
    
    // Simulate user activity
    for (int i = 0; i < 10; i++)
    {
        await connection.InvokeAsync("MovePiece", GenerateRandomMove());
        await Task.Delay(Random.Next(100, 500));
    }
    
    return Response.Ok();
})
.WithLoadSimulations(
    Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromMinutes(5))
);
```

## Code Coverage

### Viewing Coverage Reports
```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"CoverageReport"

# Open in browser
open CoverageReport/index.html
```

### Coverage Requirements
- **Overall**: 85% minimum
- **Core Business Logic**: 95% minimum
- **Critical Paths**: 100% required
- **UI Components**: 70% minimum

## CI/CD Integration

### GitHub Actions Workflow
```yaml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
          
      - name: Run Unit Tests
        run: dotnet test tests/CollaborativePuzzle.Tests
        
      - name: Run Integration Tests
        run: dotnet test tests/CollaborativePuzzle.IntegrationTests
        
      - name: Upload Coverage
        uses: codecov/codecov-action@v3
```

## Performance Baselines

### Expected Performance Metrics

**Redis Operations:**
- Simple Get: < 1ms
- Simple Set: < 2ms
- Distributed Lock: < 5ms
- Pub/Sub: < 3ms

**SignalR Operations:**
- Connection: < 100ms
- Message Send: < 10ms
- Broadcast (100 clients): < 50ms

**API Endpoints:**
- Health Check: < 10ms
- Get Puzzles: < 50ms
- Create Session: < 100ms

## Load Testing Targets

- **Concurrent Users**: 1000+
- **Messages/Second**: 10,000+
- **Connection Pool**: 5000 connections
- **Response Time (P95)**: < 100ms

## Troubleshooting

### Common Issues

1. **Testcontainers not starting**
   - Ensure Docker is running
   - Check Docker resource limits
   - Verify port availability

2. **E2E tests failing**
   - Install Playwright browsers: `pwsh playwright.ps1 install`
   - Check if application is running
   - Review browser console logs

3. **Performance tests inconsistent**
   - Run in Release mode
   - Close other applications
   - Use consistent hardware

4. **Load tests timing out**
   - Increase connection pool size
   - Check server resources
   - Review application logs

## Best Practices

1. **Write tests first** (TDD)
2. **Keep tests isolated** - No shared state
3. **Use descriptive names** - Test_Condition_ExpectedResult
4. **Mock external dependencies** in unit tests
5. **Test edge cases** and error conditions
6. **Maintain test data** - Use builders and fixtures
7. **Run tests frequently** - Before every commit
8. **Monitor test performance** - Keep tests fast

## Future Enhancements

- [ ] Visual regression testing with Playwright
- [ ] Chaos engineering tests
- [ ] Security penetration testing
- [ ] Mobile device testing
- [ ] Accessibility testing
- [ ] Contract testing for APIs

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Playwright Documentation](https://playwright.dev/dotnet/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [NBomber Documentation](https://nbomber.com/)