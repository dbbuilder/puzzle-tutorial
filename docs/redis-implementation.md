# Redis Implementation Guide

## Overview
Redis (Remote Dictionary Server) is an in-memory data structure store used as a database, cache, and message broker. In our puzzle platform, Redis serves as both a high-performance cache and the backbone for SignalR scaling.

## Purpose in Our Application
- **SignalR Backplane** for horizontal scaling across multiple server instances
- **Session State Caching** for fast user session retrieval
- **Puzzle Data Caching** for frequently accessed puzzle information
- **Real-time Pub/Sub** for cross-service communication
- **Rate Limiting** for API and WebSocket connections
- **Distributed Locking** for puzzle piece conflict resolution

## Architecture Implementation

### 1. Redis Configuration
**File**: `src/CollaborativePuzzle.Infrastructure/Configuration/RedisConfiguration.cs`

```csharp
public class RedisConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "PuzzlePlatform";
    public int Database { get; set; } = 0;
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromHours(1);
    public bool EnableKeyspaceNotifications { get; set; } = true;
    public int CommandTimeout { get; set; } = 5000;
    public int ConnectTimeout { get; set; } = 5000;
    public bool AbortOnConnectFail { get; set; } = false;
    public int ConnectRetry { get; set; } = 3;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}
```

### 2. Redis Service Implementation
**File**: `src/CollaborativePuzzle.Infrastructure/Services/RedisService.cs`

```csharp
public class RedisService : IRedisService, IDisposable
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisService> _logger;
    private readonly RedisConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public RedisService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisService> logger,
        IOptions<RedisConfiguration> config)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase(_config.Database);
        _subscriber = connectionMultiplexer.GetSubscriber();
        _logger = logger;
        _config = config.Value;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
    
    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            var fullKey = BuildKey(key);
            var expiryToUse = expiry ?? _config.DefaultExpiry;
            
            var result = await _database.StringSetAsync(fullKey, value, expiryToUse);
            
            _logger.LogDebug("Set Redis key {Key} with expiry {Expiry}", fullKey, expiryToUse);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Redis key {Key}", key);
            return false;
        }
    }
    
    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            var fullKey = BuildKey(key);
            var value = await _database.StringGetAsync(fullKey);
            
            if (value.HasValue)
            {
                _logger.LogDebug("Retrieved Redis key {Key}", fullKey);
                return value;
            }
            
            _logger.LogDebug("Redis key {Key} not found", fullKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Redis key {Key}", key);
            return null;
        }
    }
    
    public async Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            return await SetStringAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize and set Redis object for key {Key}", key);
            return false;
        }
    }
    
    public async Task<T?> GetObjectAsync<T>(string key)
    {
        try
        {
            var json = await GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return default;
            
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get and deserialize Redis object for key {Key}", key);
            return default;
        }
    }
}
```

### 3. SignalR Backplane Configuration
**File**: `src/CollaborativePuzzle.Api/Program.cs`

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure Redis connection
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Redis");
            
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 5000;
            options.CommandTimeout = 5000;
            options.ConnectRetry = 3;
            
            // Enable keyspace notifications for session management
            options.ConfigurationChannel = "__keyevent@0__:expired";
            
            return ConnectionMultiplexer.Connect(options);
        });
        
        // Configure SignalR with Redis backplane
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        })
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithSecurity(MessagePackSecurity.UntrustedData)
                .WithCompression(MessagePackCompression.Lz4Block);
        })
        .AddStackExchangeRedis(connectionString =>
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            return configuration.GetConnectionString("Redis");
        });
    }
}
```

## SignalR Backplane Implementation

### 1. Redis Backplane Architecture
```
Server Instance 1 ──┐
                    ├── Redis Pub/Sub ──┐
Server Instance 2 ──┤                   ├── All Connected Clients
                    ├── Channels        ├── (WebSocket/SSE/Long Polling)
Server Instance N ──┘                   │
                                        │
Client Connections ─────────────────────┘
```

### 2. Custom SignalR User ID Provider
**File**: `src/CollaborativePuzzle.Hubs/UserIdProvider.cs`

```csharp
public class CustomUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        // Extract user ID from JWT claims
        var userIdClaim = connection.User?.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim?.Value ?? string.Empty;
    }
}
```

### 3. Redis-based Group Management
**File**: `src/CollaborativePuzzle.Hubs/Services/RedisGroupManager.cs`

```csharp
public class RedisGroupManager : IGroupManager
{
    private readonly IRedisService _redisService;
    private readonly ILogger<RedisGroupManager> _logger;
    
    public async Task AddToGroupAsync(string connectionId, string groupName)
    {
        var key = $"signalr:groups:{groupName}";
        var memberKey = $"signalr:members:{connectionId}";
        
        // Add connection to group set
        await _redisService.SetAddAsync(key, connectionId);
        
        // Track group membership for connection
        await _redisService.SetAddAsync(memberKey, groupName);
        
        // Set expiration for automatic cleanup
        await _redisService.ExpireAsync(key, TimeSpan.FromHours(24));
        await _redisService.ExpireAsync(memberKey, TimeSpan.FromHours(24));
        
        _logger.LogDebug("Added connection {ConnectionId} to group {GroupName}", 
            connectionId, groupName);
    }
    
    public async Task RemoveFromGroupAsync(string connectionId, string groupName)
    {
        var key = $"signalr:groups:{groupName}";
        var memberKey = $"signalr:members:{connectionId}";
        
        // Remove connection from group set
        await _redisService.SetRemoveAsync(key, connectionId);
        
        // Remove group from connection's membership
        await _redisService.SetRemoveAsync(memberKey, groupName);
        
        _logger.LogDebug("Removed connection {ConnectionId} from group {GroupName}", 
            connectionId, groupName);
    }
    
    public async Task<IEnumerable<string>> GetGroupMembersAsync(string groupName)
    {
        var key = $"signalr:groups:{groupName}";
        var members = await _redisService.SetMembersAsync(key);
        
        return members.Where(m => !string.IsNullOrEmpty(m));
    }
}
```

## Session State Management

### 1. User Session Caching
**File**: `src/CollaborativePuzzle.Infrastructure/Services/SessionCacheService.cs`

```csharp
public class SessionCacheService : ISessionCacheService
{
    private readonly IRedisService _redisService;
    private readonly ILogger<SessionCacheService> _logger;
    
    public async Task<UserSession?> GetUserSessionAsync(Guid userId)
    {
        var key = $"user:session:{userId}";
        var session = await _redisService.GetObjectAsync<UserSession>(key);
        
        if (session != null)
        {
            // Update last accessed time
            session.LastAccessedAt = DateTimeOffset.UtcNow;
            await SetUserSessionAsync(userId, session);
        }
        
        return session;
    }
    
    public async Task SetUserSessionAsync(Guid userId, UserSession session)
    {
        var key = $"user:session:{userId}";
        var expiry = TimeSpan.FromHours(8); // 8-hour session timeout
        
        await _redisService.SetObjectAsync(key, session, expiry);
        
        // Also maintain a set of active users for monitoring
        await _redisService.SetAddAsync("active:users", userId.ToString());
        
        _logger.LogDebug("Cached session for user {UserId}", userId);
    }
    
    public async Task InvalidateUserSessionAsync(Guid userId)
    {
        var key = $"user:session:{userId}";
        await _redisService.DeleteAsync(key);
        
        // Remove from active users set
        await _redisService.SetRemoveAsync("active:users", userId.ToString());
        
        _logger.LogInformation("Invalidated session for user {UserId}", userId);
    }
    
    public async Task<IEnumerable<Guid>> GetActiveUsersAsync()
    {
        var activeUserStrings = await _redisService.SetMembersAsync("active:users");
        
        return activeUserStrings
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse);
    }
}
```

### 2. Puzzle Data Caching
**File**: `src/CollaborativePuzzle.Infrastructure/Services/PuzzleCacheService.cs`

```csharp
public class PuzzleCacheService : IPuzzleCacheService
{
    private readonly IRedisService _redisService;
    private readonly IPuzzleRepository _puzzleRepository;
    private readonly ILogger<PuzzleCacheService> _logger;
    
    public async Task<PuzzleDto?> GetPuzzleAsync(Guid puzzleId)
    {
        var key = $"puzzle:{puzzleId}";
        var cached = await _redisService.GetObjectAsync<PuzzleDto>(key);
        
        if (cached != null)
        {
            _logger.LogDebug("Retrieved puzzle {PuzzleId} from cache", puzzleId);
            return cached;
        }
        
        // Cache miss - fetch from database
        var puzzle = await _puzzleRepository.GetPuzzleByIdAsync(puzzleId);
        if (puzzle != null)
        {
            var puzzleDto = MapToDto(puzzle);
            
            // Cache for 1 hour
            await _redisService.SetObjectAsync(key, puzzleDto, TimeSpan.FromHours(1));
            
            _logger.LogDebug("Cached puzzle {PuzzleId} from database", puzzleId);
            return puzzleDto;
        }
        
        return null;
    }
    
    public async Task InvalidatePuzzleCacheAsync(Guid puzzleId)
    {
        var keys = new[]
        {
            $"puzzle:{puzzleId}",
            $"puzzle:pieces:{puzzleId}",
            $"puzzle:sessions:{puzzleId}"
        };
        
        await Task.WhenAll(keys.Select(key => _redisService.DeleteAsync(key)));
        
        _logger.LogInformation("Invalidated cache for puzzle {PuzzleId}", puzzleId);
    }
    
    public async Task WarmupPuzzleCacheAsync(Guid puzzleId)
    {
        // Pre-load puzzle data into cache
        await GetPuzzleAsync(puzzleId);
        await GetPuzzlePiecesAsync(puzzleId);
        
        _logger.LogInformation("Warmed up cache for puzzle {PuzzleId}", puzzleId);
    }
}
```

## Rate Limiting Implementation

### 1. Redis-based Rate Limiter
**File**: `src/CollaborativePuzzle.Infrastructure/Services/RedisRateLimiter.cs`

```csharp
public class RedisRateLimiter : IRateLimiter
{
    private readonly IRedisService _redisService;
    private readonly ILogger<RedisRateLimiter> _logger;
    
    public async Task<RateLimitResult> CheckRateLimitAsync(
        string identifier, 
        string operation, 
        int maxRequests, 
        TimeSpan window)
    {
        var key = $"ratelimit:{operation}:{identifier}";
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.Subtract(window);
        
        // Use Redis sorted set for sliding window rate limiting
        var script = @"
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local limit = tonumber(ARGV[3])
            local windowStart = now - window
            
            -- Remove expired entries
            redis.call('zremrangebyscore', key, '-inf', windowStart)
            
            -- Count current requests in window
            local current = redis.call('zcard', key)
            
            if current < limit then
                -- Add current request
                redis.call('zadd', key, now, now)
                redis.call('expire', key, window)
                return {1, limit - current - 1}
            else
                return {0, 0}
            end
        ";
        
        var result = await _redisService.ScriptEvaluateAsync(script, 
            new RedisKey[] { key },
            new RedisValue[] { now.ToUnixTimeMilliseconds(), window.TotalMilliseconds, maxRequests });
        
        var resultArray = (RedisValue[])result;
        var allowed = (int)resultArray[0] == 1;
        var remaining = (int)resultArray[1];
        
        return new RateLimitResult
        {
            IsAllowed = allowed,
            RemainingRequests = remaining,
            RetryAfter = allowed ? TimeSpan.Zero : CalculateRetryAfter(key, window)
        };
    }
}
```

### 2. Rate Limiting Middleware
**File**: `src/CollaborativePuzzle.Api/Middleware/RateLimitingMiddleware.cs`

```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var rateLimitAttribute = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();
        
        if (rateLimitAttribute != null)
        {
            var identifier = GetClientIdentifier(context);
            var operation = GetOperationName(context);
            
            var result = await _rateLimiter.CheckRateLimitAsync(
                identifier, 
                operation, 
                rateLimitAttribute.MaxRequests, 
                rateLimitAttribute.Window);
            
            if (!result.IsAllowed)
            {
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.Headers.Add("Retry-After", 
                    result.RetryAfter.TotalSeconds.ToString());
                    
                await context.Response.WriteAsync("Rate limit exceeded");
                return;
            }
        }
        
        await _next(context);
    }
}
```

## Distributed Locking

### 1. Redis Distributed Lock
**File**: `src/CollaborativePuzzle.Infrastructure/Services/RedisDistributedLock.cs`

```csharp
public class RedisDistributedLock : IDistributedLock
{
    private readonly IRedisService _redisService;
    private readonly ILogger<RedisDistributedLock> _logger;
    
    public async Task<IDisposable?> AcquireLockAsync(
        string resource, 
        TimeSpan expiry, 
        TimeSpan timeout)
    {
        var lockKey = $"lock:{resource}";
        var lockValue = Guid.NewGuid().ToString();
        var startTime = DateTimeOffset.UtcNow;
        
        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            // Try to acquire lock using SET with NX and EX options
            var acquired = await _redisService.StringSetAsync(
                lockKey, 
                lockValue, 
                expiry, 
                When.NotExists);
            
            if (acquired)
            {
                _logger.LogDebug("Acquired distributed lock for resource {Resource}", resource);
                return new RedisLock(_redisService, lockKey, lockValue, _logger);
            }
            
            // Wait a bit before retrying
            await Task.Delay(50);
        }
        
        _logger.LogWarning("Failed to acquire distributed lock for resource {Resource} within timeout", resource);
        return null;
    }
}

public class RedisLock : IDisposable
{
    private readonly IRedisService _redisService;
    private readonly string _lockKey;
    private readonly string _lockValue;
    private readonly ILogger _logger;
    private bool _disposed = false;
    
    public RedisLock(IRedisService redisService, string lockKey, string lockValue, ILogger logger)
    {
        _redisService = redisService;
        _lockKey = lockKey;
        _lockValue = lockValue;
        _logger = logger;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            // Release lock only if we still own it
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end
            ";
            
            var result = _redisService.ScriptEvaluateAsync(script,
                new RedisKey[] { _lockKey },
                new RedisValue[] { _lockValue }).GetAwaiter().GetResult();
            
            if ((int)result == 1)
            {
                _logger.LogDebug("Released distributed lock {LockKey}", _lockKey);
            }
            else
            {
                _logger.LogWarning("Failed to release distributed lock {LockKey} - may have expired", _lockKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing distributed lock {LockKey}", _lockKey);
        }
        
        _disposed = true;
    }
}
```

### 2. Puzzle Piece Locking Service
**File**: `src/CollaborativePuzzle.Core/Services/PieceLockingService.cs`

```csharp
public class PieceLockingService : IPieceLockingService
{
    private readonly IDistributedLock _distributedLock;
    private readonly IRedisService _redisService;
    private readonly ILogger<PieceLockingService> _logger;
    
    public async Task<bool> TryLockPieceAsync(Guid pieceId, Guid userId, TimeSpan lockDuration)
    {
        var lockResource = $"piece:{pieceId}";
        
        using var distributedLock = await _distributedLock.AcquireLockAsync(
            lockResource, 
            lockDuration, 
            TimeSpan.FromSeconds(5));
        
        if (distributedLock == null)
        {
            _logger.LogWarning("Could not acquire distributed lock for piece {PieceId}", pieceId);
            return false;
        }
        
        // Check if piece is already locked by another user
        var currentLockKey = $"piece:lock:{pieceId}";
        var currentLock = await _redisService.GetStringAsync(currentLockKey);
        
        if (!string.IsNullOrEmpty(currentLock) && currentLock != userId.ToString())
        {
            return false; // Already locked by another user
        }
        
        // Lock the piece
        await _redisService.SetStringAsync(currentLockKey, userId.ToString(), lockDuration);
        
        // Track locks by user for cleanup purposes
        var userLocksKey = $"user:locks:{userId}";
        await _redisService.SetAddAsync(userLocksKey, pieceId.ToString());
        await _redisService.ExpireAsync(userLocksKey, lockDuration);
        
        _logger.LogDebug("Locked piece {PieceId} for user {UserId}", pieceId, userId);
        return true;
    }
    
    public async Task ReleasePieceLockAsync(Guid pieceId, Guid userId)
    {
        var lockKey = $"piece:lock:{pieceId}";
        var userLocksKey = $"user:locks:{userId}";
        
        // Use Lua script to ensure atomic unlock
        var script = @"
            local lockKey = KEYS[1]
            local userLocksKey = KEYS[2]
            local userId = ARGV[1]
            local pieceId = ARGV[2]
            
            local currentLock = redis.call('get', lockKey)
            if currentLock == userId then
                redis.call('del', lockKey)
                redis.call('srem', userLocksKey, pieceId)
                return 1
            else
                return 0
            end
        ";
        
        var result = await _redisService.ScriptEvaluateAsync(script,
            new RedisKey[] { lockKey, userLocksKey },
            new RedisValue[] { userId.ToString(), pieceId.ToString() });
        
        if ((int)result == 1)
        {
            _logger.LogDebug("Released piece lock {PieceId} for user {UserId}", pieceId, userId);
        }
        else
        {
            _logger.LogWarning("Failed to release piece lock {PieceId} for user {UserId} - not owner", pieceId, userId);
        }
    }
}
```

## Pub/Sub for Cross-Service Communication

### 1. Redis Pub/Sub Service
**File**: `src/CollaborativePuzzle.Infrastructure/Services/RedisPubSubService.cs`

```csharp
public class RedisPubSubService : IPubSubService, IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisPubSubService> _logger;
    private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _subscriptions = new();
    
    public async Task PublishAsync(string channel, object message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var subscribersNotified = await _subscriber.PublishAsync(channel, json);
            
            _logger.LogDebug("Published message to channel {Channel}, {Subscribers} subscribers notified", 
                channel, subscribersNotified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to channel {Channel}", channel);
        }
    }
    
    public async Task SubscribeAsync(string channel, Func<string, Task> handler)
    {
        try
        {
            _subscriptions.AddOrUpdate(channel,
                new List<Func<string, Task>> { handler },
                (key, existing) =>
                {
                    existing.Add(handler);
                    return existing;
                });
            
            await _subscriber.SubscribeAsync(channel, async (ch, message) =>
            {
                if (_subscriptions.TryGetValue(channel, out var handlers))
                {
                    await Task.WhenAll(handlers.Select(h => h(message)));
                }
            });
            
            _logger.LogDebug("Subscribed to channel {Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to channel {Channel}", channel);
        }
    }
}
```

### 2. Session Event Broadcasting
**File**: `src/CollaborativePuzzle.Core/Services/SessionEventService.cs`

```csharp
public class SessionEventService : ISessionEventService
{
    private readonly IPubSubService _pubSubService;
    private readonly ILogger<SessionEventService> _logger;
    
    public async Task PublishSessionEventAsync(Guid sessionId, SessionEvent sessionEvent)
    {
        var channel = $"session:events:{sessionId}";
        
        var eventMessage = new
        {
            SessionId = sessionId,
            EventType = sessionEvent.Type,
            UserId = sessionEvent.UserId,
            Data = sessionEvent.Data,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        await _pubSubService.PublishAsync(channel, eventMessage);
        
        _logger.LogDebug("Published session event {EventType} for session {SessionId}", 
            sessionEvent.Type, sessionId);
    }
    
    public async Task SubscribeToSessionEventsAsync(Guid sessionId, Func<SessionEvent, Task> handler)
    {
        var channel = $"session:events:{sessionId}";
        
        await _pubSubService.SubscribeAsync(channel, async message =>
        {
            try
            {
                var eventData = JsonSerializer.Deserialize<SessionEvent>(message);
                if (eventData != null)
                {
                    await handler(eventData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process session event for session {SessionId}", sessionId);
            }
        });
    }
}
```

## Performance Monitoring and Health Checks

### 1. Redis Health Check
**File**: `src/CollaborativePuzzle.Infrastructure/HealthChecks/RedisHealthCheck.cs`

```csharp
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisHealthCheck> _logger;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            
            // Test basic connectivity
            var pingResult = await database.PingAsync();
            
            // Test write/read operation
            var testKey = "healthcheck:test";
            var testValue = Guid.NewGuid().ToString();
            
            await database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            var retrievedValue = await database.StringGetAsync(testKey);
            
            if (retrievedValue != testValue)
            {
                return HealthCheckResult.Unhealthy("Redis read/write test failed");
            }
            
            // Clean up test key
            await database.KeyDeleteAsync(testKey);
            
            var data = new Dictionary<string, object>
            {
                ["ping"] = pingResult.TotalMilliseconds,
                ["server"] = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First()).ToString()
            };
            
            return HealthCheckResult.Healthy("Redis is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy("Redis health check failed", ex);
        }
    }
}
```

### 2. Redis Performance Metrics
**File**: `src/CollaborativePuzzle.Infrastructure/Services/RedisMetricsService.cs`

```csharp
public class RedisMetricsService : IRedisMetricsService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IMetrics _metrics;
    
    public async Task CollectMetricsAsync()
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            // Connection metrics
            var connectedClients = info.FirstOrDefault(i => i.Key == "connected_clients")?.Value ?? "0";
            _metrics.CreateGauge<int>("redis_connected_clients")
                .Record(int.Parse(connectedClients));
            
            // Memory metrics
            var usedMemory = info.FirstOrDefault(i => i.Key == "used_memory")?.Value ?? "0";
            _metrics.CreateGauge<long>("redis_memory_used_bytes")
                .Record(long.Parse(usedMemory));
            
            // Operation metrics
            var totalCommandsProcessed = info.FirstOrDefault(i => i.Key == "total_commands_processed")?.Value ?? "0";
            _metrics.CreateCounter<long>("redis_commands_total")
                .Add(long.Parse(totalCommandsProcessed));
            
            // Keyspace metrics
            var database = _connectionMultiplexer.GetDatabase();
            var dbSize = await database.ExecuteAsync("DBSIZE");
            _metrics.CreateGauge<long>("redis_keys_total")
                .Record((long)dbSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect Redis metrics");
        }
    }
}
```

This comprehensive Redis implementation provides robust caching, SignalR scaling, session management, rate limiting, and distributed locking capabilities essential for the collaborative puzzle platform's performance and scalability.
