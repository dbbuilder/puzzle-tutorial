# Monitoring & Observability Guide

## Overview
This guide covers comprehensive monitoring, logging, and observability practices for the Collaborative Puzzle Platform, essential for maintaining enterprise-grade applications.

## The Three Pillars of Observability

### 1. Metrics
Numerical measurements collected at regular intervals.

### 2. Logs
Discrete events with detailed context.

### 3. Traces
End-to-end journey of requests through the system.

## Application Insights Setup

### Basic Configuration
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnablePerformanceCounterCollectionModule = true;
    options.EnableRequestTrackingTelemetryModule = true;
    options.EnableDependencyTrackingTelemetryModule = true;
});

// Custom telemetry initializer
builder.Services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

// Configure telemetry processors
builder.Services.AddApplicationInsightsTelemetryProcessor<CustomTelemetryProcessor>();
```

### Custom Telemetry
```csharp
public class CustomTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public void Initialize(ITelemetry telemetry)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            // Add user ID
            telemetry.Context.User.Id = context.User?.Identity?.Name;
            
            // Add session ID
            telemetry.Context.Session.Id = context.Request.Headers["X-Session-Id"];
            
            // Add custom properties
            if (telemetry is ISupportProperties properties)
            {
                properties.Properties["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                properties.Properties["Version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                properties.Properties["NodeName"] = Environment.MachineName;
            }
        }
    }
}
```

## Structured Logging with Serilog

### Configuration
```csharp
// Program.cs
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "CollaborativePuzzle")
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces)
        .WriteTo.Async(a => a.File(
            new JsonFormatter(),
            "logs/app-.json",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));
});

// Middleware for request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"]);
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress);
    };
});
```

### Logging Best Practices
```csharp
public class PuzzleService
{
    private readonly ILogger<PuzzleService> _logger;
    
    public async Task<PuzzleDto> GetPuzzleAsync(string puzzleId)
    {
        using var activity = Activity.StartActivity("GetPuzzle");
        activity?.SetTag("puzzle.id", puzzleId);
        
        try
        {
            _logger.LogInformation(
                "Retrieving puzzle {PuzzleId} for user {UserId}",
                puzzleId,
                _currentUser.Id);
            
            var stopwatch = Stopwatch.StartNew();
            var puzzle = await _repository.GetByIdAsync(puzzleId);
            
            if (puzzle == null)
            {
                _logger.LogWarning(
                    "Puzzle {PuzzleId} not found",
                    puzzleId);
                throw new NotFoundException($"Puzzle {puzzleId} not found");
            }
            
            _logger.LogInformation(
                "Successfully retrieved puzzle {PuzzleId} in {ElapsedMs}ms",
                puzzleId,
                stopwatch.ElapsedMilliseconds);
            
            return puzzle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving puzzle {PuzzleId}",
                puzzleId);
            throw;
        }
    }
}
```

## Custom Metrics

### Business Metrics
```csharp
public class MetricsService
{
    private readonly TelemetryClient _telemetryClient;
    
    public void TrackPuzzleStarted(string puzzleId, string sessionId)
    {
        _telemetryClient.TrackEvent("PuzzleStarted", new Dictionary<string, string>
        {
            ["PuzzleId"] = puzzleId,
            ["SessionId"] = sessionId
        });
        
        _telemetryClient.GetMetric("ActivePuzzleSessions").TrackValue(1);
    }
    
    public void TrackPieceMove(string sessionId, double moveTime)
    {
        _telemetryClient.GetMetric("PieceMoveTime", "SessionId")
            .TrackValue(moveTime, sessionId);
    }
    
    public void TrackPuzzleCompleted(string puzzleId, int pieces, TimeSpan duration)
    {
        _telemetryClient.TrackEvent("PuzzleCompleted", 
            new Dictionary<string, string>
            {
                ["PuzzleId"] = puzzleId,
                ["Pieces"] = pieces.ToString()
            },
            new Dictionary<string, double>
            {
                ["DurationMinutes"] = duration.TotalMinutes
            });
    }
}
```

### Performance Metrics
```csharp
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryClient _telemetryClient;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Track request duration
            _telemetryClient.GetMetric("RequestDuration", "Endpoint", "StatusCode")
                .TrackValue(stopwatch.ElapsedMilliseconds,
                    context.Request.Path,
                    context.Response.StatusCode.ToString());
            
            // Track slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _telemetryClient.TrackEvent("SlowRequest",
                    new Dictionary<string, string>
                    {
                        ["Path"] = context.Request.Path,
                        ["Method"] = context.Request.Method,
                        ["StatusCode"] = context.Response.StatusCode.ToString()
                    },
                    new Dictionary<string, double>
                    {
                        ["Duration"] = stopwatch.ElapsedMilliseconds
                    });
            }
        }
    }
}
```

## Distributed Tracing

### OpenTelemetry Setup
```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("CollaborativePuzzle")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                }))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRedisInstrumentation()
            .AddSqlClientInstrumentation()
            .AddSource("CollaborativePuzzle")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"]);
            });
    });
```

### Custom Spans
```csharp
public class PuzzleGenerationService
{
    private static readonly ActivitySource ActivitySource = new("CollaborativePuzzle");
    
    public async Task<IEnumerable<PuzzlePiece>> GeneratePieces(string puzzleId, Stream imageStream)
    {
        using var activity = ActivitySource.StartActivity("GeneratePuzzlePieces", ActivityKind.Internal);
        activity?.SetTag("puzzle.id", puzzleId);
        activity?.SetTag("image.size", imageStream.Length);
        
        try
        {
            // Image processing
            using (var imageProcessingSpan = ActivitySource.StartActivity("ProcessImage"))
            {
                var processedImage = await ProcessImage(imageStream);
                imageProcessingSpan?.SetTag("processed.width", processedImage.Width);
                imageProcessingSpan?.SetTag("processed.height", processedImage.Height);
            }
            
            // Piece generation
            using (var generationSpan = ActivitySource.StartActivity("GeneratePieces"))
            {
                var pieces = await GeneratePieceData(processedImage);
                generationSpan?.SetTag("piece.count", pieces.Count());
                return pieces;
            }
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Health Checks

### Comprehensive Health Monitoring
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: configuration.GetConnectionString("DefaultConnection"),
        name: "sql-server",
        tags: new[] { "db", "sql" })
    .AddRedis(
        configuration["Redis:ConnectionString"],
        name: "redis",
        tags: new[] { "cache", "redis" })
    .AddAzureBlobStorage(
        connectionString: configuration["AzureStorage:ConnectionString"],
        containerName: "puzzles",
        name: "blob-storage",
        tags: new[] { "storage" })
    .AddCheck<SignalRHealthCheck>("signalr", tags: new[] { "signalr" })
    .AddCheck<CustomHealthCheck>("custom", tags: new[] { "custom" });

// Health check UI
builder.Services.AddHealthChecksUI(options =>
{
    options.SetEvaluationTimeInSeconds(30);
    options.MaximumHistoryEntriesPerEndpoint(50);
    options.AddHealthCheckEndpoint("API", "/health");
})
.AddInMemoryStorage();
```

### Custom Health Checks
```csharp
public class SignalRHealthCheck : IHealthCheck
{
    private readonly IHubContext<PuzzleHub> _hubContext;
    private readonly IConnectionMultiplexer _redis;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        
        try
        {
            // Check SignalR connections
            var connectionCount = PuzzleHub.ConnectionCount;
            data["ActiveConnections"] = connectionCount;
            
            if (connectionCount > 10000)
            {
                return HealthCheckResult.Degraded(
                    "High connection count",
                    data: data);
            }
            
            // Check Redis pub/sub
            var subscriber = _redis.GetSubscriber();
            var latency = await MeasureRedisLatency(subscriber);
            data["RedisLatency"] = latency;
            
            if (latency > 100)
            {
                return HealthCheckResult.Degraded(
                    $"High Redis latency: {latency}ms",
                    data: data);
            }
            
            return HealthCheckResult.Healthy("SignalR is healthy", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "SignalR check failed",
                exception: ex,
                data: data);
        }
    }
}
```

## Alerting Rules

### Application Insights Alerts
```json
{
  "alerts": [
    {
      "name": "High Error Rate",
      "condition": "exceptions/count > 10",
      "timeWindow": "5 minutes",
      "severity": "Critical",
      "actions": ["email", "slack"]
    },
    {
      "name": "Slow Response Time",
      "condition": "requests/duration > 1000",
      "percentile": 95,
      "timeWindow": "10 minutes",
      "severity": "Warning"
    },
    {
      "name": "Low Cache Hit Rate",
      "condition": "customMetrics/CacheHitRate < 0.8",
      "timeWindow": "15 minutes",
      "severity": "Warning"
    },
    {
      "name": "High Memory Usage",
      "condition": "performanceCounters/memoryAvailableBytes < 500000000",
      "timeWindow": "5 minutes",
      "severity": "Critical"
    }
  ]
}
```

## Dashboards

### KQL Queries for Application Insights
```kql
// Request performance by endpoint
requests
| where timestamp > ago(1h)
| summarize 
    avg_duration = avg(duration),
    p95_duration = percentile(duration, 95),
    p99_duration = percentile(duration, 99),
    count = count()
    by name
| order by p95_duration desc

// Error rate by operation
exceptions
| where timestamp > ago(1h)
| summarize error_count = count() by operation_Name, type
| join kind=leftouter (
    requests
    | where timestamp > ago(1h)
    | summarize total_count = count() by operation_Name
) on operation_Name
| extend error_rate = todouble(error_count) / todouble(total_count) * 100
| order by error_rate desc

// SignalR connection metrics
customMetrics
| where timestamp > ago(1h)
| where name == "ActiveConnections"
| summarize avg_connections = avg(value), max_connections = max(value) by bin(timestamp, 1m)
| render timechart

// Cache performance
customMetrics
| where timestamp > ago(1h)
| where name in ("CacheHits", "CacheMisses")
| summarize hits = sumif(value, name == "CacheHits"), 
           misses = sumif(value, name == "CacheMisses") by bin(timestamp, 5m)
| extend hit_rate = todouble(hits) / (todouble(hits) + todouble(misses)) * 100
| render timechart
```

### Grafana Dashboard Configuration
```json
{
  "dashboard": {
    "title": "Collaborative Puzzle Platform",
    "panels": [
      {
        "title": "Request Rate",
        "query": "sum(rate(http_requests_total[5m])) by (endpoint)"
      },
      {
        "title": "Error Rate",
        "query": "sum(rate(http_requests_total{status=~\"5..\"}[5m])) / sum(rate(http_requests_total[5m]))"
      },
      {
        "title": "Response Time",
        "query": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))"
      },
      {
        "title": "Active WebSocket Connections",
        "query": "websocket_connections_active"
      },
      {
        "title": "Redis Operations",
        "query": "sum(rate(redis_commands_total[5m])) by (command)"
      }
    ]
  }
}
```

## Performance Profiling

### CPU Profiling
```csharp
public class ProfilingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DiagnosticSource _diagnosticSource;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Enable-Profiling"))
        {
            using var activity = Activity.StartActivity("ProfiledRequest");
            
            var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
            var memoryBefore = GC.GetTotalMemory(false);
            
            await _next(context);
            
            var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
            var memoryAfter = GC.GetTotalMemory(false);
            
            var cpuUsed = (cpuAfter - cpuBefore).TotalMilliseconds;
            var memoryUsed = memoryAfter - memoryBefore;
            
            context.Response.Headers.Add("X-CPU-Time", cpuUsed.ToString());
            context.Response.Headers.Add("X-Memory-Used", memoryUsed.ToString());
            
            _diagnosticSource.Write("RequestProfile", new
            {
                Path = context.Request.Path,
                CpuTime = cpuUsed,
                MemoryUsed = memoryUsed
            });
        }
        else
        {
            await _next(context);
        }
    }
}
```

## Log Aggregation

### Centralized Logging Pattern
```csharp
public class LogAggregationService
{
    private readonly ILogger<LogAggregationService> _logger;
    private readonly TelemetryClient _telemetryClient;
    
    public void LogBusinessEvent(string eventName, object data)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["EventName"] = eventName,
            ["Timestamp"] = DateTime.UtcNow
        }))
        {
            // Log to Serilog
            _logger.LogInformation("Business event: {EventName} with data: {@Data}", eventName, data);
            
            // Send to Application Insights
            _telemetryClient.TrackEvent(eventName, 
                data.GetType().GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(data)?.ToString()));
            
            // Send to custom analytics
            SendToAnalytics(eventName, data, correlationId);
        }
    }
}
```

## Production Debugging

### Remote Debugging Configuration
```csharp
// Enable detailed errors in production (temporarily)
if (app.Environment.IsProduction() && 
    configuration.GetValue<bool>("EnableDetailedErrors"))
{
    app.UseDeveloperExceptionPage();
}

// Conditional debugging endpoints
app.MapGet("/debug/connections", async (IHubContext<PuzzleHub> hub) =>
{
    if (!IsDebugAuthorized(context))
        return Results.Forbid();
        
    return Results.Ok(new
    {
        ActiveConnections = PuzzleHub.ConnectionCount,
        ConnectionDetails = PuzzleHub.GetConnectionDetails()
    });
}).RequireAuthorization("DebugPolicy");
```

## Cost Optimization

### Telemetry Sampling
```csharp
public class AdaptiveTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    private readonly Random _random = new();
    
    public void Process(ITelemetry item)
    {
        // Sample based on telemetry type
        var sampleRate = item switch
        {
            TraceTelemetry trace when trace.SeverityLevel == SeverityLevel.Verbose => 0.1,
            DependencyTelemetry dep when dep.Duration < TimeSpan.FromMilliseconds(10) => 0.05,
            RequestTelemetry req when req.ResponseCode == "200" => 0.5,
            _ => 1.0
        };
        
        if (_random.NextDouble() < sampleRate)
        {
            _next.Process(item);
        }
    }
}
```

## Summary
Comprehensive monitoring and observability are critical for maintaining enterprise applications. This guide provides patterns and practices for:
- Structured logging with correlation
- Custom metrics and business KPIs
- Distributed tracing
- Health monitoring
- Performance profiling
- Cost-effective telemetry collection

Remember: You can't fix what you can't measure!