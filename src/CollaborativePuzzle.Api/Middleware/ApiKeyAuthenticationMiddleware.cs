using System.Security.Claims;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace CollaborativePuzzle.Api.Middleware;

/// <summary>
/// Middleware for API key authentication
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ApiKeyQueryName = "api_key";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeyService)
    {
        // Skip if already authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Try to get API key from header or query string
        var apiKey = GetApiKey(context.Request);
        if (string.IsNullOrEmpty(apiKey))
        {
            await _next(context);
            return;
        }

        try
        {
            // Validate the API key
            var validationResult = await apiKeyService.ValidateApiKeyAsync(apiKey);
            if (validationResult.IsValid)
            {
                // Create claims for the API key user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, validationResult.UserId!),
                    new Claim("AuthenticationMethod", "ApiKey")
                };

                // Add scope claims
                foreach (var scope in validationResult.Scopes!)
                {
                    claims.Add(new Claim("scope", scope));
                }

                var identity = new ClaimsIdentity(claims, "ApiKey");
                var principal = new ClaimsPrincipal(identity);
                
                // Set the user context
                context.User = principal;
                
                _logger.LogDebug("API key authenticated for user {UserId}", validationResult.UserId);
            }
            else
            {
                _logger.LogWarning("Invalid API key attempted: {Error}", validationResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during API key authentication");
        }

        await _next(context);
    }

    private static string? GetApiKey(HttpRequest request)
    {
        // Try header first
        if (request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
        {
            return apiKeyHeader.FirstOrDefault();
        }

        // Try query string
        if (request.Query.TryGetValue(ApiKeyQueryName, out var apiKeyQuery))
        {
            return apiKeyQuery.FirstOrDefault();
        }

        // Try Authorization header with Bearer scheme (for compatibility)
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer cp_", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring(7); // Remove "Bearer " prefix
        }

        return null;
    }
}

/// <summary>
/// Extension methods for API key authentication middleware
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}