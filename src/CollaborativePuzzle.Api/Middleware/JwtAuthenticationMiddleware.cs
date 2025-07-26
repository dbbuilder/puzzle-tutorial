using CollaborativePuzzle.Core.Interfaces;

namespace CollaborativePuzzle.Api.Middleware;

/// <summary>
/// Middleware for validating JWT tokens and setting the user context
/// </summary>
public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public JwtAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = ExtractTokenFromHeader(context);

        if (!string.IsNullOrEmpty(token))
        {
            var jwtService = context.RequestServices.GetRequiredService<IJwtService>();
            var validationResult = jwtService.ValidateToken(token);

            if (validationResult.IsValid && validationResult.Principal != null)
            {
                context.User = validationResult.Principal;
            }
        }

        await _next(context);
    }

    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader))
        {
            return null;
        }

        // Check if it starts with "Bearer " (case insensitive)
        const string bearerPrefix = "Bearer ";
        if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring(bearerPrefix.Length).Trim();
        }

        return null;
    }
}