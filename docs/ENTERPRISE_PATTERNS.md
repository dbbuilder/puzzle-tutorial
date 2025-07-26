# Enterprise Architecture Patterns

## Overview
This guide explains common enterprise architecture patterns used in the Collaborative Puzzle Platform and how they apply to real-world systems.

## Architectural Patterns

### 1. Microservices Architecture

#### Pattern Description
The platform uses a microservices approach where different functionalities are separated into distinct services that communicate through well-defined interfaces.

#### Implementation in Our Platform
```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   API Gateway   │────▶│  Puzzle Service  │────▶│   SQL Database  │
└────────┬────────┘     └──────────────────┘     └─────────────────┘
         │                        │
         │              ┌──────────────────┐     ┌─────────────────┐
         ├──────────────│ SignalR Service  │────▶│  Redis Cache    │
         │              └──────────────────┘     └─────────────────┘
         │                        │
         │              ┌──────────────────┐     ┌─────────────────┐
         └──────────────│  Media Service   │────▶│  Blob Storage   │
                        └──────────────────┘     └─────────────────┘
```

#### Benefits
- Independent deployment and scaling
- Technology diversity (different services can use different tech stacks)
- Fault isolation
- Team autonomy

#### Code Example
```csharp
// API Gateway - Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddHttpClient("PuzzleService", client =>
{
    client.BaseAddress = new Uri(configuration["Services:PuzzleService"]);
});

builder.Services.AddHttpClient("MediaService", client =>
{
    client.BaseAddress = new Uri(configuration["Services:MediaService"]);
});

// Gateway endpoint that orchestrates multiple services
app.MapGet("/api/puzzle/{id}/full", async (
    string id,
    IHttpClientFactory httpClientFactory) =>
{
    var puzzleClient = httpClientFactory.CreateClient("PuzzleService");
    var mediaClient = httpClientFactory.CreateClient("MediaService");
    
    // Parallel calls to different services
    var puzzleTask = puzzleClient.GetAsync($"/puzzle/{id}");
    var mediaTask = mediaClient.GetAsync($"/media/puzzle/{id}");
    
    await Task.WhenAll(puzzleTask, mediaTask);
    
    // Combine responses
    var puzzle = await puzzleTask.Result.Content.ReadFromJsonAsync<PuzzleDto>();
    var media = await mediaTask.Result.Content.ReadFromJsonAsync<MediaDto>();
    
    return Results.Ok(new { puzzle, media });
});
```

### 2. Event-Driven Architecture

#### Pattern Description
Components communicate through events, enabling loose coupling and scalability.

#### Implementation
```csharp
// Event Publisher - When puzzle piece is moved
public class PuzzleHub : Hub
{
    private readonly IConnectionMultiplexer _redis;
    
    public async Task MovePiece(MovePieceCommand command)
    {
        // Validate move
        if (!IsValidMove(command))
            return;
        
        // Update database
        await _puzzleService.UpdatePiecePosition(command);
        
        // Publish event to Redis
        var @event = new PieceMoved
        {
            SessionId = command.SessionId,
            PieceId = command.PieceId,
            NewPosition = command.Position,
            MovedBy = Context.UserIdentifier,
            Timestamp = DateTime.UtcNow
        };
        
        await _redis.GetSubscriber().PublishAsync(
            $"puzzle:{command.SessionId}",
            JsonSerializer.Serialize(@event)
        );
    }
}

// Event Subscriber - Background service
public class PuzzleEventProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        
        await subscriber.SubscribeAsync("puzzle:*", async (channel, message) =>
        {
            var @event = JsonSerializer.Deserialize<PuzzleEvent>(message);
            
            switch (@event)
            {
                case PieceMoved pieceMoved:
                    await HandlePieceMoved(pieceMoved);
                    break;
                case PuzzleCompleted puzzleCompleted:
                    await HandlePuzzleCompleted(puzzleCompleted);
                    break;
            }
        });
    }
}
```

### 3. CQRS (Command Query Responsibility Segregation)

#### Pattern Description
Separate read and write operations to optimize for different concerns.

#### Write Side (Commands)
```csharp
public interface ICommand { }

public record CreatePuzzleSession(
    string PuzzleId,
    string CreatedBy,
    int MaxPlayers
) : ICommand;

public class CreatePuzzleSessionHandler
{
    private readonly IDbConnection _writeDb;
    private readonly IEventBus _eventBus;
    
    public async Task<string> Handle(CreatePuzzleSession command)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        // Write to primary database
        await _writeDb.ExecuteAsync(
            "sp_CreatePuzzleSession",
            new
            {
                SessionId = sessionId,
                command.PuzzleId,
                command.CreatedBy,
                command.MaxPlayers
            },
            commandType: CommandType.StoredProcedure
        );
        
        // Publish event
        await _eventBus.PublishAsync(new PuzzleSessionCreated
        {
            SessionId = sessionId,
            PuzzleId = command.PuzzleId
        });
        
        return sessionId;
    }
}
```

#### Read Side (Queries)
```csharp
public interface IQuery<TResult> { }

public record GetActiveSessions() : IQuery<IEnumerable<SessionSummary>>;

public class GetActiveSessionsHandler
{
    private readonly IRedisCache _cache;
    private readonly IDbConnection _readDb;
    
    public async Task<IEnumerable<SessionSummary>> Handle(GetActiveSessions query)
    {
        // Try cache first
        var cached = await _cache.GetAsync<IEnumerable<SessionSummary>>("active-sessions");
        if (cached != null)
            return cached;
        
        // Query read-optimized view
        var sessions = await _readDb.QueryAsync<SessionSummary>(
            @"SELECT s.SessionId, s.PuzzleId, p.Name as PuzzleName,
                     s.PlayerCount, s.StartedAt, s.LastActivity
              FROM SessionSummaryView s
              JOIN Puzzles p ON s.PuzzleId = p.Id
              WHERE s.IsActive = 1
              ORDER BY s.PlayerCount DESC"
        );
        
        // Cache for 30 seconds
        await _cache.SetAsync("active-sessions", sessions, TimeSpan.FromSeconds(30));
        
        return sessions;
    }
}
```

### 4. Repository Pattern with Unit of Work

#### Pattern Description
Abstracts data access logic and provides a more object-oriented view of the persistence layer.

#### Implementation
```csharp
// Repository Interface
public interface IPuzzleRepository
{
    Task<Puzzle> GetByIdAsync(string id);
    Task<IEnumerable<Puzzle>> GetActiveAsync();
    Task CreateAsync(Puzzle puzzle);
    Task UpdateAsync(Puzzle puzzle);
}

// Unit of Work
public interface IUnitOfWork : IDisposable
{
    IPuzzleRepository Puzzles { get; }
    ISessionRepository Sessions { get; }
    IPieceRepository Pieces { get; }
    
    Task<int> CommitAsync();
    Task RollbackAsync();
}

// Implementation
public class SqlUnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction _transaction;
    
    public SqlUnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();
        
        Puzzles = new PuzzleRepository(_connection, _transaction);
        Sessions = new SessionRepository(_connection, _transaction);
        Pieces = new PieceRepository(_connection, _transaction);
    }
    
    public async Task<int> CommitAsync()
    {
        try
        {
            _transaction.Commit();
            return 0;
        }
        catch
        {
            _transaction.Rollback();
            throw;
        }
    }
}

// Usage in service
public class PuzzleService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<string> CreatePuzzleWithPieces(
        CreatePuzzleDto dto,
        Stream imageStream)
    {
        try
        {
            // Create puzzle
            var puzzle = new Puzzle
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                CreatedBy = dto.UserId
            };
            
            await _unitOfWork.Puzzles.CreateAsync(puzzle);
            
            // Generate pieces
            var pieces = await GeneratePieces(imageStream, puzzle.Id);
            foreach (var piece in pieces)
            {
                await _unitOfWork.Pieces.CreateAsync(piece);
            }
            
            // Commit transaction
            await _unitOfWork.CommitAsync();
            
            return puzzle.Id;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
```

### 5. Circuit Breaker Pattern

#### Pattern Description
Prevents cascading failures by detecting failures and preventing calls to failing services.

#### Implementation with Polly
```csharp
// Configure circuit breaker
services.AddHttpClient<IPuzzleServiceClient>()
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            3,                        // Number of failures before opening circuit
            TimeSpan.FromSeconds(30), // Duration of open circuit
            onBreak: (result, duration) =>
            {
                Log.Warning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reset");
            });
}

// Usage
public class PuzzleGateway
{
    private readonly HttpClient _httpClient;
    private readonly ICircuitBreaker _circuitBreaker;
    
    public async Task<PuzzleDto> GetPuzzleAsync(string id)
    {
        try
        {
            // Circuit breaker will throw if circuit is open
            var response = await _httpClient.GetAsync($"/api/puzzles/{id}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<PuzzleDto>();
        }
        catch (BrokenCircuitException)
        {
            // Return cached or default data
            Log.Warning("Circuit is open, returning cached data");
            return await GetCachedPuzzle(id) ?? new PuzzleDto { Id = id, Name = "Unavailable" };
        }
    }
}
```

### 6. Saga Pattern for Distributed Transactions

#### Pattern Description
Manages distributed transactions across multiple services using a sequence of local transactions.

#### Implementation
```csharp
public class CreatePuzzleSessionSaga
{
    private readonly IMediaService _mediaService;
    private readonly IPuzzleService _puzzleService;
    private readonly INotificationService _notificationService;
    
    public async Task<SagaResult> Execute(CreateSessionCommand command)
    {
        var sagaId = Guid.NewGuid();
        var compensations = new Stack<Func<Task>>();
        
        try
        {
            // Step 1: Upload media
            var mediaId = await _mediaService.UploadAsync(command.Image);
            compensations.Push(async () => await _mediaService.DeleteAsync(mediaId));
            
            // Step 2: Create puzzle
            var puzzleId = await _puzzleService.CreateAsync(new CreatePuzzleDto
            {
                MediaId = mediaId,
                Name = command.Name
            });
            compensations.Push(async () => await _puzzleService.DeleteAsync(puzzleId));
            
            // Step 3: Create session
            var sessionId = await _puzzleService.CreateSessionAsync(puzzleId);
            compensations.Push(async () => await _puzzleService.DeleteSessionAsync(sessionId));
            
            // Step 4: Notify users
            await _notificationService.NotifyNewSession(sessionId);
            
            return new SagaResult { Success = true, SessionId = sessionId };
        }
        catch (Exception ex)
        {
            // Compensate in reverse order
            while (compensations.Count > 0)
            {
                var compensation = compensations.Pop();
                try
                {
                    await compensation();
                }
                catch (Exception compEx)
                {
                    Log.Error(compEx, "Compensation failed");
                }
            }
            
            return new SagaResult { Success = false, Error = ex.Message };
        }
    }
}
```

### 7. Outbox Pattern for Reliable Messaging

#### Pattern Description
Ensures messages are reliably published by storing them in the database as part of the business transaction.

#### Implementation
```csharp
public class OutboxPattern
{
    // Store events in outbox table
    public async Task CreatePuzzleWithOutbox(CreatePuzzleDto dto)
    {
        using var transaction = await _connection.BeginTransactionAsync();
        
        try
        {
            // Business operation
            var puzzleId = await _connection.ExecuteScalarAsync<string>(
                "sp_CreatePuzzle",
                new { dto.Name, dto.CreatedBy },
                transaction,
                commandType: CommandType.StoredProcedure
            );
            
            // Store event in outbox
            var @event = new PuzzleCreatedEvent
            {
                PuzzleId = puzzleId,
                Name = dto.Name,
                CreatedAt = DateTime.UtcNow
            };
            
            await _connection.ExecuteAsync(
                @"INSERT INTO Outbox (Id, EventType, Payload, CreatedAt)
                  VALUES (@Id, @EventType, @Payload, @CreatedAt)",
                new
                {
                    Id = Guid.NewGuid(),
                    EventType = @event.GetType().Name,
                    Payload = JsonSerializer.Serialize(@event),
                    CreatedAt = DateTime.UtcNow
                },
                transaction
            );
            
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    // Background service to publish events
    public class OutboxProcessor : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var events = await _connection.QueryAsync<OutboxEvent>(
                    @"SELECT TOP 100 * FROM Outbox 
                      WHERE ProcessedAt IS NULL 
                      ORDER BY CreatedAt"
                );
                
                foreach (var @event in events)
                {
                    try
                    {
                        await _eventBus.PublishAsync(@event.EventType, @event.Payload);
                        
                        await _connection.ExecuteAsync(
                            "UPDATE Outbox SET ProcessedAt = @Now WHERE Id = @Id",
                            new { Now = DateTime.UtcNow, @event.Id }
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to process outbox event {EventId}", @event.Id);
                    }
                }
                
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

## Best Practices

### 1. Idempotency
Ensure operations can be safely retried:
```csharp
public async Task<IActionResult> CreatePuzzle(
    [FromBody] CreatePuzzleDto dto,
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
{
    // Check if request was already processed
    var existing = await _cache.GetAsync<string>($"idempotency:{idempotencyKey}");
    if (existing != null)
        return Ok(new { puzzleId = existing });
    
    var puzzleId = await _puzzleService.CreateAsync(dto);
    
    // Store result with expiration
    await _cache.SetAsync($"idempotency:{idempotencyKey}", puzzleId, TimeSpan.FromHours(24));
    
    return Ok(new { puzzleId });
}
```

### 2. Graceful Degradation
```csharp
public async Task<SessionDetails> GetSessionDetails(string sessionId)
{
    var details = new SessionDetails { SessionId = sessionId };
    
    // Core data - fail if not available
    details.Session = await _sessionService.GetAsync(sessionId) 
        ?? throw new NotFoundException();
    
    // Enhancement data - continue if not available
    try
    {
        details.Statistics = await _statsService.GetSessionStats(sessionId);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to load statistics");
        details.Statistics = new Statistics { Available = false };
    }
    
    return details;
}
```

### 3. Health Checks
```csharp
public class PuzzleServiceHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check database
            await _connection.ExecuteScalarAsync("SELECT 1");
            
            // Check Redis
            await _redis.GetDatabase().PingAsync();
            
            // Check blob storage
            var properties = await _blobContainer.GetPropertiesAsync();
            
            return HealthCheckResult.Healthy("All systems operational");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Service degraded", ex);
        }
    }
}
```

## Summary
These patterns form the foundation of enterprise-grade applications. They provide:
- **Scalability**: Handle increased load through horizontal scaling
- **Reliability**: Graceful failure handling and recovery
- **Maintainability**: Clear separation of concerns
- **Flexibility**: Easy to modify and extend
- **Observability**: Built-in monitoring and debugging capabilities