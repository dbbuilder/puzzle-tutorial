using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.MinimalApis;

/// <summary>
/// Minimal API endpoints for authentication
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/auth")
            .WithTags("Authentication")
            .WithOpenApi()
            .AllowAnonymous()
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1, 0);

        // POST /api/v1/auth/login
        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("User login")
            .WithDescription("Authenticates a user with username and password")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // POST /api/v1/auth/register
        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("User registration")
            .WithDescription("Creates a new user account")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        // POST /api/v1/auth/refresh
        group.MapPost("/refresh", RefreshTokenAsync)
            .WithName("RefreshToken")
            .WithSummary("Refresh JWT token")
            .WithDescription("Refreshes an expired JWT token")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // GET /api/v1/auth/me
        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Get current user")
            .WithDescription("Returns information about the authenticated user")
            .Produces<UserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        // POST /api/v1/auth/logout
        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("User logout")
            .WithDescription("Logs out the current user")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        // Azure AD B2C endpoints
        var b2cGroup = app.MapGroup("/api/v1/auth/b2c")
            .WithTags("Azure AD B2C")
            .WithOpenApi()
            .AllowAnonymous();

        // POST /api/v1/auth/b2c/login
        b2cGroup.MapPost("/login", B2CLoginAsync)
            .WithName("B2CLogin")
            .WithSummary("Login with Azure AD B2C token")
            .WithDescription("Authenticates using an Azure AD B2C token")
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status200OK)
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status401Unauthorized);

        // POST /api/v1/auth/b2c/callback
        b2cGroup.MapPost("/callback", B2CCallbackAsync)
            .WithName("B2CCallback")
            .WithSummary("Azure AD B2C callback")
            .WithDescription("Handles the Azure AD B2C authorization code callback")
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status200OK)
            .Produces<AzureAdB2CAuthResponse>(StatusCodes.Status401Unauthorized);

        // GET /api/v1/auth/b2c/login-url
        b2cGroup.MapGet("/login-url", GetB2CLoginUrl)
            .WithName("GetB2CLoginUrl")
            .WithSummary("Get Azure AD B2C login URL")
            .WithDescription("Returns the URL to initiate Azure AD B2C login")
            .Produces<object>(StatusCodes.Status200OK);

        // GET /api/v1/auth/b2c/logout-url
        b2cGroup.MapGet("/logout-url", GetB2CLogoutUrl)
            .WithName("GetB2CLogoutUrl")
            .WithSummary("Get Azure AD B2C logout URL")
            .WithDescription("Returns the URL to logout from Azure AD B2C")
            .Produces<object>(StatusCodes.Status200OK);

        // V2 endpoints with enhanced features
        var v2Group = app.MapGroup("/api/v{version:apiVersion}/auth")
            .WithTags("Authentication V2")
            .WithOpenApi()
            .AllowAnonymous()
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(2, 0);

        // POST /api/v2/auth/login with multi-factor authentication
        v2Group.MapPost("/login", LoginV2Async)
            .WithName("LoginV2")
            .WithSummary("User login with MFA support")
            .WithDescription("Authenticates a user with enhanced security features")
            .Produces<LoginResponseV2>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // POST /api/v2/auth/mfa/verify
        v2Group.MapPost("/mfa/verify", VerifyMfaAsync)
            .WithName("VerifyMFA")
            .WithSummary("Verify MFA code")
            .WithDescription("Verifies the multi-factor authentication code")
            .Produces<LoginResponseV2>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // POST /api/v2/auth/token/revoke
        v2Group.MapPost("/token/revoke", RevokeTokenAsync)
            .WithName("RevokeToken")
            .WithSummary("Revoke access token")
            .WithDescription("Revokes the current access token")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        // GET /api/v2/auth/session/active
        v2Group.MapGet("/session/active", GetActiveSessionsAsync)
            .WithName("GetActiveSessions")
            .WithSummary("Get active sessions")
            .WithDescription("Returns all active sessions for the authenticated user")
            .Produces<ActiveSessionsResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        IUserService userService = null!,
        IJwtService jwtService = null!,
        ILogger<Program> logger = null!)
    {
        var result = await userService.ValidateCredentialsAsync(request.Username, request.Password);
        
        if (!result.Success || result.User == null)
        {
            logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return TypedResults.Unauthorized();
        }
        
        var token = jwtService.GenerateToken(result.User, result.Roles.ToArray());
        
        logger.LogInformation("User {UserId} logged in successfully", result.User.Id);
        
        return TypedResults.Ok(new LoginResponse
        {
            Success = true,
            Token = token,
            User = new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email,
                Roles = result.Roles.ToArray().ToArray()
            }
        });
    }

    private static async Task<Results<Created<RegisterResponse>, BadRequest<ValidationProblemDetails>>> RegisterAsync(
        RegisterRequest request,
        IUserService userService = null!,
        IJwtService jwtService = null!,
        ILogger<Program> logger = null!)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "Password mismatch",
                Status = StatusCodes.Status400BadRequest,
                Errors = { ["ConfirmPassword"] = new[] { "Passwords do not match" } }
            });
        }
        
        var result = await userService.CreateUserAsync(request);
        
        if (!result.Success || result.User == null)
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "Registration failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error
            });
        }
        
        var roles = new[] { "User" };
        var token = jwtService.GenerateToken(result.User, roles);
        
        logger.LogInformation("New user registered: {UserId}", result.User.Id);
        
        return TypedResults.Created($"/api/v1/auth/me", new RegisterResponse
        {
            Success = true,
            Token = token,
            User = new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email,
                Roles = roles
            }
        });
    }

    private static Results<Ok<LoginResponse>, UnauthorizedHttpResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        IJwtService jwtService = null!,
        ILogger<Program> logger = null!)
    {
        var result = jwtService.RefreshToken(request.Token);
        
        if (!result.IsSuccess || string.IsNullOrEmpty(result.Token))
        {
            logger.LogWarning("Failed token refresh attempt");
            return TypedResults.Unauthorized();
        }
        
        logger.LogInformation("Token refreshed successfully");
        
        return TypedResults.Ok(new LoginResponse
        {
            Success = true,
            Token = result.Token
        });
    }

    private static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal user,
        IUserService userService = null!,
        ILogger<Program> logger = null!)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return TypedResults.Unauthorized();
        }
        
        var userEntity = await userService.GetUserByIdAsync(userIdClaim);
        if (userEntity == null)
        {
            return TypedResults.Unauthorized();
        }
        
        var roles = await userService.GetUserRolesAsync(userIdClaim);
        
        return TypedResults.Ok(new UserDto
        {
            Id = userEntity.Id,
            Username = userEntity.Username,
            Email = userEntity.Email,
            Roles = roles.ToArray()
        });
    }

    private static NoContent LogoutAsync(
        ClaimsPrincipal user,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        logger.LogInformation("User {UserId} logged out", userId);
        
        // In a real implementation, you might want to blacklist the token
        // or perform other cleanup operations
        
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AzureAdB2CAuthResponse>, UnauthorizedHttpResult>> B2CLoginAsync(
        AzureAdB2CLoginRequest request,
        IExternalAuthenticationService authService = null!,
        ILogger<Program> logger = null!)
    {
        try
        {
            var result = await authService.ValidateTokenAsync(request.B2CToken);
            
            if (!result.Success)
            {
                logger.LogWarning("Failed Azure AD B2C login attempt: {Error}", result.Error);
                return TypedResults.Unauthorized();
            }
            
            logger.LogInformation("User {UserId} logged in via Azure AD B2C", result.User!.Id);
            
            return TypedResults.Ok(new AzureAdB2CAuthResponse
            {
                Success = true,
                Token = result.Token,
                User = new UserDto
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Roles = result.Roles.ToArray()
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Azure AD B2C login");
            return TypedResults.Unauthorized();
        }
    }

    private static async Task<Results<Ok<AzureAdB2CAuthResponse>, UnauthorizedHttpResult>> B2CCallbackAsync(
        AzureAdB2CCallbackRequest request,
        IExternalAuthenticationService authService = null!,
        ILogger<Program> logger = null!)
    {
        try
        {
            var result = await authService.ExchangeCodeAsync(request.Code, request.RedirectUri);
            
            if (!result.Success)
            {
                logger.LogWarning("Failed Azure AD B2C code exchange: {Error}", result.Error);
                return TypedResults.Unauthorized();
            }
            
            logger.LogInformation("User {UserId} authenticated via Azure AD B2C callback", result.User!.Id);
            
            return TypedResults.Ok(new AzureAdB2CAuthResponse
            {
                Success = true,
                Token = result.Token,
                User = new UserDto
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Roles = result.Roles.ToArray()
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Azure AD B2C callback");
            return TypedResults.Unauthorized();
        }
    }

    private static IResult GetB2CLoginUrl(
        [FromQuery] string redirectUri,
        [FromQuery] string? state,
        IExternalAuthenticationService authService = null!)
    {
        var loginUrl = authService.GetLoginUrl(redirectUri, state);
        return TypedResults.Ok(new { loginUrl });
    }

    private static IResult GetB2CLogoutUrl(
        [FromQuery] string redirectUri,
        IExternalAuthenticationService authService = null!)
    {
        var logoutUrl = authService.GetLogoutUrl(redirectUri);
        return TypedResults.Ok(new { logoutUrl });
    }

    // V2 Methods Implementation
    private static async Task<Results<Ok<LoginResponseV2>, UnauthorizedHttpResult>> LoginV2Async(
        LoginRequestV2 request,
        IUserService userService = null!,
        IJwtService jwtService = null!,
        ILogger<Program> logger = null!)
    {
        var result = await userService.ValidateCredentialsAsync(request.Username, request.Password);
        
        if (!result.Success || result.User == null)
        {
            logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return TypedResults.Unauthorized();
        }
        
        // V2 simplified - MFA would be implemented later
        var token = jwtService.GenerateToken(result.User, result.Roles.ToArray());
        var refreshToken = token; // Simplified for now
        
        logger.LogInformation("User {UserId} logged in successfully (V2)", result.User.Id);
        
        return TypedResults.Ok(new LoginResponseV2
        {
            Success = true,
            Token = token,
            RefreshToken = refreshToken,
            TokenExpiry = DateTime.UtcNow.AddHours(1),
            User = new UserDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email,
                Roles = result.Roles.ToArray()
            },
            SessionId = Guid.NewGuid() // Track session
        });
    }

    private static async Task<Results<Ok<LoginResponseV2>, UnauthorizedHttpResult>> VerifyMfaAsync(
        MfaVerificationRequest request,
        IJwtService jwtService = null!,
        IUserService userService = null!,
        ILogger<Program> logger = null!)
    {
        // Simplified MFA verification - would be implemented later
        logger.LogWarning("MFA verification not implemented");
        return TypedResults.Unauthorized();
    }

    private static NoContent RevokeTokenAsync(
        ClaimsPrincipal user,
        IJwtService jwtService = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var jti = user.FindFirst("jti")?.Value;
        
        // Token revocation would be implemented with Redis blacklist
        logger.LogInformation("Token revocation requested for user {UserId}", userId);
        
        return TypedResults.NoContent();
    }

    private static Ok<ActiveSessionsResponse> GetActiveSessionsAsync(
        ClaimsPrincipal user,
        IUserService userService = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Simplified implementation - would be tracked in Redis
        var sessions = new List<UserSession>();
        
        if (!string.IsNullOrEmpty(userId))
        {
            sessions.Add(new UserSession
            {
                SessionId = Guid.NewGuid().ToString(),
                DeviceInfo = "Current Device",
                IpAddress = "127.0.0.1",
                Location = "Unknown",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                LastActivityAt = DateTime.UtcNow,
                IsCurrent = true
            });
        }
        
        return TypedResults.Ok(new ActiveSessionsResponse
        {
            Sessions = sessions
        });
    }
}

// V2 DTOs
/// <summary>
/// Enhanced login request with device info
/// </summary>
public class LoginRequestV2 : LoginRequest
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceType { get; set; }
}

/// <summary>
/// Enhanced login response with MFA and session tracking
/// </summary>
public class LoginResponseV2 : LoginResponse
{
    public bool RequiresMfa { get; set; }
    public string? MfaChallengeToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public Guid? SessionId { get; set; }
}

/// <summary>
/// MFA verification request
/// </summary>
public class MfaVerificationRequest
{
    [Required]
    public string MfaChallengeToken { get; set; } = default!;
    
    [Required]
    [MinLength(6)]
    [MaxLength(6)]
    public string MfaCode { get; set; } = default!;
}

/// <summary>
/// Active sessions response
/// </summary>
public class ActiveSessionsResponse
{
    public IEnumerable<UserSession> Sessions { get; set; } = new List<UserSession>();
}

/// <summary>
/// User session information
/// </summary>
public class UserSession
{
    public string SessionId { get; set; } = default!;
    public string DeviceInfo { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsCurrent { get; set; }
}