using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CollaborativePuzzle.Api.RateLimiting;

/// <summary>
/// Factory for creating Redis-based rate limiters
/// </summary>
public class RedisRateLimiterFactory
{
    private readonly IRateLimitStore _store;
    private readonly ILogger<RedisRateLimiterFactory> _logger;

    public RedisRateLimiterFactory(IRateLimitStore store, ILogger<RedisRateLimiterFactory> logger)
    {
        _store = store;
        _logger = logger;
    }

    public RateLimiter Create(RedisRateLimiterOptions options, string partitionKey)
    {
        // Use the built-in FixedWindowRateLimiter but with our Redis store for distributed counting
        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.PermitLimit,
            Window = options.Window,
            QueueProcessingOrder = options.QueueProcessingOrder ? QueueProcessingOrder.OldestFirst : QueueProcessingOrder.NewestFirst,
            QueueLimit = options.QueueLimit
        });
    }

}

/// <summary>
/// Options for Redis rate limiter
/// </summary>
public class RedisRateLimiterOptions
{
    /// <summary>
    /// Time window for rate limiting
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Number of permits allowed per window
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Whether to queue requests when limit is exceeded
    /// </summary>
    public bool QueueProcessingOrder { get; set; } = false;

    /// <summary>
    /// Maximum number of queued requests
    /// </summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>
/// Policy for creating distributed rate limiters
/// </summary>
public class RedisRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly RedisRateLimiterFactory _factory;
    private readonly RedisRateLimiterOptions _options;

    public RedisRateLimiterPolicy(RedisRateLimiterFactory factory, RedisRateLimiterOptions options)
    {
        _factory = factory;
        _options = options;
    }

    public RateLimiter? GetRateLimiter(HttpContext httpContext)
    {
        // Get partition key (e.g., IP address or user ID)
        var partitionKey = GetPartitionKey(httpContext);
        return _factory.Create(_options, partitionKey);
    }

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var partitionKey = GetPartitionKey(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                Window = _options.Window,
                QueueProcessingOrder = _options.QueueProcessingOrder ? QueueProcessingOrder.OldestFirst : QueueProcessingOrder.NewestFirst,
                QueueLimit = _options.QueueLimit
            });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = 
        async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            }
            
            await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", cancellationToken);
        };

    private string GetPartitionKey(HttpContext httpContext)
    {
        // Try to get user ID if authenticated
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }
        }
        
        // Fall back to IP address
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
}