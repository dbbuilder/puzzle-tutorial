using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.Models;

/// <summary>
/// Represents an API key for programmatic access
/// </summary>
public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string UserId { get; set; } = default!;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The actual API key value - only returned when creating a new key
    /// </summary>
    public string? Key { get; set; }
    
    /// <summary>
    /// SHA256 hash of the API key for storage
    /// </summary>
    [Required]
    public string KeyHash { get; set; } = default!;
    
    /// <summary>
    /// Scopes/permissions granted to this API key
    /// </summary>
    public string[] Scopes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether the API key is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When the API key expires (null for no expiration)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// When the API key was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the API key was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// When the API key was revoked (if applicable)
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}

/// <summary>
/// Result of API key validation
/// </summary>
public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string[]? Scopes { get; set; }
    public string? Error { get; set; }
    
    public static ApiKeyValidationResult Success(string userId, string[] scopes)
    {
        return new ApiKeyValidationResult
        {
            IsValid = true,
            UserId = userId,
            Scopes = scopes
        };
    }
    
    public static ApiKeyValidationResult Failure(string error)
    {
        return new ApiKeyValidationResult
        {
            IsValid = false,
            Error = error
        };
    }
}

/// <summary>
/// Available API scopes
/// </summary>
public static class ApiScopes
{
    public const string ReadPuzzles = "read:puzzles";
    public const string WritePuzzles = "write:puzzles";
    public const string DeletePuzzles = "delete:puzzles";
    public const string ReadSessions = "read:sessions";
    public const string WriteSessions = "write:sessions";
    public const string AdminUsers = "admin:users";
    public const string AdminSystem = "admin:system";
    
    public static readonly string[] AllScopes = new[]
    {
        ReadPuzzles,
        WritePuzzles,
        DeletePuzzles,
        ReadSessions,
        WriteSessions,
        AdminUsers,
        AdminSystem
    };
    
    public static readonly string[] DefaultScopes = new[]
    {
        ReadPuzzles,
        ReadSessions
    };
}