using System.Security.Claims;
using CollaborativePuzzle.Core.Interfaces;

namespace CollaborativePuzzle.Api.Middleware;

/// <summary>
/// Middleware to track rate limit usage and provide statistics
/// </summary>
public class RateLimitTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitTrackingMiddleware> _logger;

    public RateLimitTrackingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRedisService redisService)
    {
        var startTime = DateTime.UtcNow;
        var rateLimitKey = GetRateLimitKey(context);

        try
        {
            await _next(context);
        }
        finally
        {
            // Track rate limit usage
            await TrackRateLimitUsage(
                redisService,
                rateLimitKey,
                context.Response.StatusCode,
                DateTime.UtcNow - startTime);

            // Add rate limit headers
            await AddRateLimitHeaders(context, redisService, rateLimitKey);
        }
    }

    private static string GetRateLimitKey(HttpContext context)
    {
        // Try authenticated user first
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"ratelimit:stats:user:{userId}";
        }

        // Try API key
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            return $"ratelimit:stats:apikey:{apiKey}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ratelimit:stats:ip:{ipAddress}";
    }

    private async Task TrackRateLimitUsage(
        IRedisService redisService,
        string key,
        int statusCode,
        TimeSpan duration)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hourKey = $"{key}:{timestamp / 3600}";

            // Increment counters
            await Task.WhenAll(
                redisService.IncrementAsync($"{hourKey}:total"),
                statusCode == 429 
                    ? redisService.IncrementAsync($"{hourKey}:limited")
                    : Task.CompletedTask,
                statusCode >= 200 && statusCode < 300
                    ? redisService.IncrementAsync($"{hourKey}:success")
                    : Task.CompletedTask,
                statusCode >= 400 && statusCode < 500
                    ? redisService.IncrementAsync($"{hourKey}:client_error")
                    : Task.CompletedTask,
                statusCode >= 500
                    ? redisService.IncrementAsync($"{hourKey}:server_error")
                    : Task.CompletedTask
            );

            // Track response time
            await redisService.StringSetAsync(
                $"{hourKey}:last_request",
                $"{timestamp}:{duration.TotalMilliseconds}",
                TimeSpan.FromHours(2));

            // Set expiry
            await redisService.KeyExpireAsync(hourKey, TimeSpan.FromHours(25));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking rate limit usage");
        }
    }

    private async Task AddRateLimitHeaders(
        HttpContext context,
        IRedisService redisService,
        string rateLimitKey)
    {
        try
        {
            if (context.Response.StatusCode == 429)
            {
                // Headers already set by rate limiter
                return;
            }

            // Get current window stats
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var minuteKey = $"{rateLimitKey}:{timestamp / 60}";
            
            var currentCount = await redisService.GetLongAsync($"{minuteKey}:total");
            var limit = GetRateLimitForContext(context);
            
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = 
                Math.Max(0, limit - currentCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = 
                ((timestamp / 60 + 1) * 60).ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding rate limit headers");
        }
    }

    private int GetRateLimitForContext(HttpContext context)
    {
        // Check for API key tier
        var tier = context.User?.FindFirst("api_tier")?.Value;
        if (tier == "premium") return 10000;
        if (tier == "standard") return 1000;
        
        // Check if authenticated
        if (context.User?.Identity?.IsAuthenticated == true) return 100;
        
        // Default for anonymous
        return 60;
    }
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class RateLimitTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimitTracking(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitTrackingMiddleware>();
    }
}