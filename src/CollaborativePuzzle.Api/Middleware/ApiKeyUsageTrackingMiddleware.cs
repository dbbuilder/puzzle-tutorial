using System.Diagnostics;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Api.Middleware;

/// <summary>
/// Middleware to track API key usage metrics
/// </summary>
public class ApiKeyUsageTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyUsageTrackingMiddleware> _logger;

    public ApiKeyUsageTrackingMiddleware(RequestDelegate next, ILogger<ApiKeyUsageTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeyService)
    {
        // Only track if authenticated via API key
        if (context.User.Identity?.AuthenticationType != "ApiKey")
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Get API key info from context
            var apiKeyId = context.Items["ApiKeyId"] as string;
            var apiKey = context.Items["ApiKey"] as string;
            
            if (!string.IsNullOrEmpty(apiKeyId) && !string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    // Create a minimal API key object for tracking
                    var apiKeyObj = new ApiKey { Id = apiKeyId };
                    
                    // Track the usage
                    await apiKeyService.TrackApiKeyUsageAsync(
                        apiKeyObj,
                        context.Request.Path,
                        context.Response.StatusCode,
                        (int)stopwatch.ElapsedMilliseconds);
                    
                    _logger.LogDebug("Tracked API key usage for {KeyId}: {Path} - {StatusCode} - {ElapsedMs}ms",
                        apiKeyId, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    // Don't let tracking errors affect the response
                    _logger.LogError(ex, "Error tracking API key usage");
                }
            }
        }
    }
}

/// <summary>
/// Extension methods for API key usage tracking middleware
/// </summary>
public static class ApiKeyUsageTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyUsageTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyUsageTrackingMiddleware>();
    }
}