using System.Security.Claims;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.MinimalApis;

/// <summary>
/// Minimal API endpoints for rate limit information
/// </summary>
public static class RateLimitEndpoints
{
    public static void MapRateLimitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ratelimit")
            .WithTags("Rate Limiting")
            .WithOpenApi()
            .RequireAuthorization();

        // GET /api/ratelimit/status
        group.MapGet("/status", GetRateLimitStatusAsync)
            .WithName("GetRateLimitStatus")
            .WithSummary("Get current rate limit status")
            .WithDescription("Returns the current rate limit status for the authenticated user")
            .Produces<RateLimitStatus>(StatusCodes.Status200OK);

        // GET /api/ratelimit/history
        group.MapGet("/history", GetRateLimitHistoryAsync)
            .WithName("GetRateLimitHistory")
            .WithSummary("Get rate limit history")
            .WithDescription("Returns hourly rate limit usage for the past 24 hours")
            .Produces<RateLimitHistory>(StatusCodes.Status200OK);

        // GET /api/ratelimit/admin/user/{userId}
        group.MapGet("/admin/user/{userId}", GetUserRateLimitStatusAsync)
            .WithName("GetUserRateLimitStatus")
            .WithSummary("Get rate limit status for a specific user")
            .WithDescription("Returns rate limit information for a specific user (admin only)")
            .Produces<RateLimitStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization("RequireAdminRole");

        // POST /api/ratelimit/admin/reset/{userId}
        group.MapPost("/admin/reset/{userId}", ResetUserRateLimitAsync)
            .WithName("ResetUserRateLimit")
            .WithSummary("Reset rate limit for a user")
            .WithDescription("Resets the rate limit counters for a specific user (admin only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization("RequireAdminRole");
    }

    private static async Task<Ok<RateLimitStatus>> GetRateLimitStatusAsync(
        ClaimsPrincipal user,
        IRedisService redisService,
        ILogger<Program> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Ok(new RateLimitStatus
            {
                Limit = 60,
                Remaining = 60,
                ResetsAt = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        }

        var key = $"ratelimit:user:{userId}";
        var status = await GetRateLimitStatusForKey(redisService, key, GetLimitForUser(user));
        
        logger.LogInformation("Retrieved rate limit status for user {UserId}", userId);
        return TypedResults.Ok(status);
    }

    private static async Task<Ok<RateLimitHistory>> GetRateLimitHistoryAsync(
        ClaimsPrincipal user,
        IRedisService redisService,
        ILogger<Program> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Ok(new RateLimitHistory { Hours = new List<HourlyUsage>() });
        }

        var baseKey = $"ratelimit:stats:user:{userId}";
        var history = await GetRateLimitHistoryForKey(redisService, baseKey);
        
        logger.LogInformation("Retrieved rate limit history for user {UserId}", userId);
        return TypedResults.Ok(history);
    }

    private static async Task<Results<Ok<RateLimitStatus>, ForbidHttpResult>> GetUserRateLimitStatusAsync(
        string userId,
        IRedisService redisService,
        ILogger<Program> logger)
    {
        var key = $"ratelimit:user:{userId}";
        var status = await GetRateLimitStatusForKey(redisService, key, 100); // Default limit
        
        logger.LogInformation("Admin retrieved rate limit status for user {UserId}", userId);
        return TypedResults.Ok(status);
    }

    private static async Task<Results<NoContent, ForbidHttpResult>> ResetUserRateLimitAsync(
        string userId,
        IRedisService redisService,
        ILogger<Program> logger)
    {
        var pattern = $"ratelimit:*{userId}*";
        var keys = await redisService.GetKeysAsync(pattern);
        
        foreach (var key in keys)
        {
            await redisService.DeleteAsync(key);
        }
        
        logger.LogInformation("Admin reset rate limit for user {UserId}", userId);
        return TypedResults.NoContent();
    }

    private static async Task<RateLimitStatus> GetRateLimitStatusForKey(
        IRedisService redisService,
        string key,
        int limit)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.ToUnixTimeMilliseconds() - 60000; // 1 minute window
            
            // Get count from sorted set
            var count = await redisService.SortedSetLengthAsync(key, windowStart, now.ToUnixTimeMilliseconds());
            
            return new RateLimitStatus
            {
                Limit = limit,
                Remaining = Math.Max(0, limit - (int)count),
                ResetsAt = now.AddMinutes(1),
                WindowStart = DateTimeOffset.FromUnixTimeMilliseconds((long)windowStart),
                CurrentCount = (int)count
            };
        }
        catch
        {
            return new RateLimitStatus
            {
                Limit = limit,
                Remaining = limit,
                ResetsAt = DateTimeOffset.UtcNow.AddMinutes(1)
            };
        }
    }

    private static async Task<RateLimitHistory> GetRateLimitHistoryForKey(
        IRedisService redisService,
        string baseKey)
    {
        var history = new RateLimitHistory
        {
            Hours = new List<HourlyUsage>()
        };

        var now = DateTimeOffset.UtcNow;
        
        for (int i = 0; i < 24; i++)
        {
            var hour = now.AddHours(-i);
            var timestamp = hour.ToUnixTimeSeconds();
            var hourKey = $"{baseKey}:{timestamp / 3600}";
            
            var total = await redisService.GetAsync<long>($"{hourKey}:total");
            var limited = await redisService.GetAsync<long>($"{hourKey}:limited");
            var success = await redisService.GetAsync<long>($"{hourKey}:success");
            var clientError = await redisService.GetAsync<long>($"{hourKey}:client_error");
            var serverError = await redisService.GetAsync<long>($"{hourKey}:server_error");
            
            history.Hours.Add(new HourlyUsage
            {
                Hour = hour,
                TotalRequests = total,
                RateLimitedRequests = limited,
                SuccessfulRequests = success,
                ClientErrors = clientError,
                ServerErrors = serverError
            });
        }
        
        history.Hours = history.Hours.OrderBy(h => h.Hour).ToList();
        history.TotalRequests = history.Hours.Sum(h => h.TotalRequests);
        history.TotalRateLimited = history.Hours.Sum(h => h.RateLimitedRequests);
        
        return history;
    }

    private static int GetLimitForUser(ClaimsPrincipal user)
    {
        var tier = user.FindFirst("api_tier")?.Value;
        return tier switch
        {
            "premium" => 10000,
            "standard" => 1000,
            _ => 100
        };
    }
}

/// <summary>
/// Current rate limit status
/// </summary>
public class RateLimitStatus
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTimeOffset ResetsAt { get; set; }
    public DateTimeOffset? WindowStart { get; set; }
    public int? CurrentCount { get; set; }
}

/// <summary>
/// Rate limit usage history
/// </summary>
public class RateLimitHistory
{
    public List<HourlyUsage> Hours { get; set; } = new();
    public long TotalRequests { get; set; }
    public long TotalRateLimited { get; set; }
}

/// <summary>
/// Hourly usage statistics
/// </summary>
public class HourlyUsage
{
    public DateTimeOffset Hour { get; set; }
    public long TotalRequests { get; set; }
    public long RateLimitedRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long ClientErrors { get; set; }
    public long ServerErrors { get; set; }
}