using System.Security.Claims;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces;

/// <summary>
/// Service for handling JWT token generation and validation
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT token for the specified user with roles
    /// </summary>
    string GenerateToken(User user, params string[] roles);
    
    /// <summary>
    /// Validates a JWT token and returns the result
    /// </summary>
    TokenValidationResult ValidateToken(string token);
    
    /// <summary>
    /// Refreshes a valid token with a new expiration
    /// </summary>
    RefreshTokenResult RefreshToken(string token);
    
    /// <summary>
    /// Extracts the user ID from a token without full validation
    /// </summary>
    string? GetUserIdFromToken(string token);
    
    /// <summary>
    /// Extracts roles from a token without full validation
    /// </summary>
    IEnumerable<string> GetRolesFromToken(string token);
}

/// <summary>
/// Result of token validation
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public ClaimsPrincipal? Principal { get; set; }
    public string? Error { get; set; }
    
    public static TokenValidationResult Success(ClaimsPrincipal principal) => new()
    {
        IsValid = true,
        Principal = principal
    };
    
    public static TokenValidationResult Failure(string error) => new()
    {
        IsValid = false,
        Error = error
    };
}

/// <summary>
/// Result of token refresh operation
/// </summary>
public class RefreshTokenResult
{
    public bool IsSuccess { get; set; }
    public string? Token { get; set; }
    public string? Error { get; set; }
    
    public static RefreshTokenResult Success(string token) => new()
    {
        IsSuccess = true,
        Token = token
    };
    
    public static RefreshTokenResult Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}