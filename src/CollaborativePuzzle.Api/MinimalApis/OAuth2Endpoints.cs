using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.MinimalApis;

/// <summary>
/// Minimal API endpoints for OAuth2 authentication flows
/// </summary>
public static class OAuth2Endpoints
{
    public static void MapOAuth2Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/oauth2")
            .WithTags("OAuth2 Authentication")
            .WithOpenApi()
            .AllowAnonymous(); // OAuth2 endpoints don't require authentication

        // GET /api/oauth2/authorize
        group.MapGet("/authorize", GetAuthorizationUrl)
            .WithName("GetOAuth2AuthorizationUrl")
            .WithSummary("Get OAuth2 authorization URL")
            .WithDescription("Returns the URL to redirect users for OAuth2 authorization")
            .Produces<OAuth2AuthorizeResponse>(StatusCodes.Status200OK);

        // POST /api/oauth2/callback
        group.MapPost("/callback", HandleOAuth2Callback)
            .WithName("HandleOAuth2Callback")
            .WithSummary("Handle OAuth2 callback")
            .WithDescription("Exchanges authorization code for tokens and creates/updates user")
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // GET /api/oauth2/logout
        group.MapGet("/logout", GetLogoutUrl)
            .WithName("GetOAuth2LogoutUrl")
            .WithSummary("Get OAuth2 logout URL")
            .WithDescription("Returns the URL to redirect users for OAuth2 logout")
            .Produces<OAuth2LogoutResponse>(StatusCodes.Status200OK);

        // POST /api/oauth2/token/validate
        group.MapPost("/token/validate", ValidateExternalToken)
            .WithName("ValidateExternalToken")
            .WithSummary("Validate external OAuth2 token")
            .WithDescription("Validates an external OAuth2 token and returns user information")
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // POST /api/oauth2/refresh
        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshOAuth2Token")
            .WithSummary("Refresh OAuth2 tokens")
            .WithDescription("Uses a refresh token to obtain new access tokens")
            .Produces<OAuth2TokenResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
    }

    private static Ok<OAuth2AuthorizeResponse> GetAuthorizationUrl(
        [FromQuery] string? redirectUri,
        [FromQuery] string? state,
        IExternalAuthenticationService authService,
        IConfiguration configuration)
    {
        var defaultRedirectUri = redirectUri ?? configuration["OAuth2:DefaultRedirectUri"] ?? "https://localhost:5001/oauth2/callback";
        var authUrl = authService.GetLoginUrl(defaultRedirectUri, state);
        
        return TypedResults.Ok(new OAuth2AuthorizeResponse
        {
            AuthorizationUrl = authUrl,
            State = state ?? Guid.NewGuid().ToString()
        });
    }

    private static async Task<Results<Ok<AzureAdB2CAuthResponse>, BadRequest<ProblemDetails>>> HandleOAuth2Callback(
        AzureAdB2CCallbackRequest request,
        IExternalAuthenticationService authService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Authorization code is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await authService.ExchangeCodeAsync(request.Code, request.RedirectUri);
        
        if (!result.Success)
        {
            logger.LogWarning("OAuth2 callback failed: {Error}", result.Error);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        logger.LogInformation("OAuth2 callback successful for user {UserId}", result.User?.Id);
        
        return TypedResults.Ok(new AzureAdB2CAuthResponse
        {
            Success = true,
            Token = result.Token,
            User = result.User != null ? new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email,
                Roles = result.Roles?.ToArray() ?? Array.Empty<string>()
            } : null
        });
    }

    private static Ok<OAuth2LogoutResponse> GetLogoutUrl(
        [FromQuery] string? redirectUri,
        IExternalAuthenticationService authService,
        IConfiguration configuration)
    {
        var defaultRedirectUri = redirectUri ?? configuration["OAuth2:DefaultLogoutRedirectUri"] ?? "https://localhost:5001/";
        var logoutUrl = authService.GetLogoutUrl(defaultRedirectUri);
        
        return TypedResults.Ok(new OAuth2LogoutResponse
        {
            LogoutUrl = logoutUrl
        });
    }

    private static async Task<Results<Ok<AzureAdB2CAuthResponse>, BadRequest<ProblemDetails>>> ValidateExternalToken(
        AzureAdB2CLoginRequest request,
        IExternalAuthenticationService authService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(request.B2CToken))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Token is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await authService.ValidateTokenAsync(request.B2CToken);
        
        if (!result.Success)
        {
            logger.LogWarning("Token validation failed: {Error}", result.Error);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Token Validation Failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        logger.LogInformation("Token validation successful for user {UserId}", result.User?.Id);
        
        return TypedResults.Ok(new AzureAdB2CAuthResponse
        {
            Success = true,
            Token = result.Token,
            User = result.User != null ? new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email,
                Roles = result.Roles?.ToArray() ?? Array.Empty<string>()
            } : null
        });
    }

    private static async Task<Results<Ok<OAuth2TokenResponse>, BadRequest<ProblemDetails>>> RefreshToken(
        OAuth2RefreshTokenRequest request,
        IJwtService jwtService,
        ILogger<Program> logger)
    {
        // In a complete implementation, this would:
        // 1. Validate the refresh token
        // 2. Check if it's expired or revoked
        // 3. Generate new access and refresh tokens
        // 4. Optionally rotate the refresh token
        
        logger.LogInformation("Refresh token request received");
        
        // For now, return a placeholder response
        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Not Implemented",
            Detail = "Refresh token functionality requires additional implementation",
            Status = StatusCodes.Status501NotImplemented
        });
    }
}

/// <summary>
/// Response containing OAuth2 authorization URL
/// </summary>
public class OAuth2AuthorizeResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Response containing OAuth2 logout URL
/// </summary>
public class OAuth2LogoutResponse
{
    public string LogoutUrl { get; set; } = string.Empty;
}

/// <summary>
/// Request to refresh OAuth2 tokens
/// </summary>
public class OAuth2RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Response containing OAuth2 tokens
/// </summary>
public class OAuth2TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}