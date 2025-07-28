using System.Security.Claims;
using System.Threading.RateLimiting;
using CollaborativePuzzle.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace CollaborativePuzzle.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds Redis-based rate limiting to the service collection
    /// </summary>
    public static IServiceCollection AddRedisRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: partition => new SlidingWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 1000,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 100
                    }));

            // Fixed window policy for general API endpoints
            options.AddPolicy("fixed", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // Sliding window policy for high-traffic endpoints
            options.AddPolicy("sliding", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: partition => new SlidingWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 20
                    }));

            // Concurrency limiter for resource-intensive endpoints
            options.AddPolicy("concurrent", context =>
                RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: partition => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // Token bucket for burst traffic
            options.AddPolicy("burst", context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: GetPartitionKey(context),
                    factory: partition => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 100,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        TokensPerPeriod = 20,
                        AutoReplenishment = true,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 50
                    }));

            // API key specific rate limiting
            options.AddPolicy("apikey", httpContext =>
            {
                var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault();
                if (string.IsNullOrEmpty(apiKey))
                {
                    // No API key - strict limits
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                }

                // API key present - check tier from claims
                var tier = httpContext.User.FindFirst("api_tier")?.Value ?? "basic";
                
                return tier switch
                {
                    "premium" => RateLimitPartition.GetNoLimiter(apiKey),
                    "standard" => RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: apiKey,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 100
                        }),
                    _ => RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: apiKey,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 10
                        })
                };
            });

            // Redis-based distributed rate limiting
            options.AddPolicy("redis", httpContext =>
            {
                var serviceProvider = httpContext.RequestServices;
                var store = serviceProvider.GetRequiredService<IRateLimitStore>();
                var partitionKey = GetPartitionKey(httpContext);

                // Use fixed window limiter with Redis store
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    factory: key => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 50
                    });
            });

            // Handler for rate limit exceeded
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = 
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsync(
                    "Rate limit exceeded. Please try again later.",
                    cancellationToken: token);
            };
        });

        return services;
    }

    /// <summary>
    /// Gets the partition key for rate limiting based on the HTTP context
    /// </summary>
    private static string GetPartitionKey(HttpContext context)
    {
        // Try authenticated user first
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Try API key
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            return $"apikey:{apiKey}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
}