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
    
    // Enhanced features
    /// <summary>
    /// Rate limit tier for this API key (basic, standard, premium, unlimited)
    /// </summary>
    public string RateLimitTier { get; set; } = "basic";
    
    /// <summary>
    /// ID of the API key this was rotated from
    /// </summary>
    public string? RotatedFromKeyId { get; set; }
    
    /// <summary>
    /// When this API key was rotated
    /// </summary>
    public DateTime? RotatedAt { get; set; }
    
    /// <summary>
    /// Custom metadata for the API key
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Custom rate limits (overrides tier defaults)
    /// </summary>
    public int? MaxRequestsPerMinute { get; set; }
    public int? MaxRequestsPerHour { get; set; }
    public int? MaxRequestsPerDay { get; set; }
}

/// <summary>
/// Result of API key validation
/// </summary>
public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string? ApiKeyId { get; set; }
    public string[]? Scopes { get; set; }
    public string? Error { get; set; }
    
    // Enhanced features
    public RateLimitInfo? RateLimitInfo { get; set; }
    public bool IsNearExpiration { get; set; }
    public int? DaysUntilExpiration { get; set; }
    
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
/// Rate limit information for API key
/// </summary>
public class RateLimitInfo
{
    public string Tier { get; set; } = "basic";
    public int Limit { get; set; }
    public int CurrentUsage { get; set; }
    public TimeSpan Window { get; set; }
    public DateTime ResetsAt { get; set; }
}

/// <summary>
/// Available API scopes
/// </summary>
public static class ApiScopes
{
    // Legacy scope names (for backward compatibility)
    public const string ReadPuzzles = "read_puzzles";
    public const string WritePuzzles = "write_puzzles";
    public const string DeletePuzzles = "delete_puzzles";
    public const string ReadSessions = "read_sessions";
    public const string WriteSessions = "write_sessions";
    public const string AdminUsers = "admin_users";
    public const string AdminSystem = "admin_system";
    
    // Hierarchical scope names
    public const string PuzzlesRead = "puzzles:read";
    public const string PuzzlesWrite = "puzzles:write";
    public const string PuzzlesDelete = "puzzles:delete";
    public const string PuzzlesAll = "puzzles:*";
    
    public const string SessionsRead = "sessions:read";
    public const string SessionsWrite = "sessions:write";
    public const string SessionsAll = "sessions:*";
    
    public const string AdminUsersRead = "admin:users:read";
    public const string AdminUsersWrite = "admin:users:write";
    public const string AdminUsersAll = "admin:users:*";
    public const string AdminSystemAll = "admin:system:*";
    public const string AdminAll = "admin:*";
    
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
    
    /// <summary>
    /// Maps hierarchical scopes to their expanded permissions
    /// </summary>
    public static readonly Dictionary<string, string[]> ScopeHierarchy = new()
    {
        ["puzzles:*"] = new[] { ReadPuzzles, WritePuzzles, DeletePuzzles },
        ["puzzles:read"] = new[] { ReadPuzzles },
        ["puzzles:write"] = new[] { ReadPuzzles, WritePuzzles },
        ["sessions:*"] = new[] { ReadSessions, WriteSessions },
        ["sessions:read"] = new[] { ReadSessions },
        ["sessions:write"] = new[] { ReadSessions, WriteSessions },
        ["admin:users:*"] = new[] { AdminUsers },
        ["admin:system:*"] = new[] { AdminSystem },
        ["admin:*"] = new[] { AdminUsers, AdminSystem }
    };
}