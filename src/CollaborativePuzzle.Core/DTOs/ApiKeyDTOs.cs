using System.ComponentModel.DataAnnotations;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.DTOs;

/// <summary>
/// Request to create a new API key
/// </summary>
public class CreateApiKeyRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;
    
    [Required]
    public string[] Scopes { get; set; } = default!;
    
    /// <summary>
    /// Number of days until the key expires (null for no expiration)
    /// </summary>
    [Range(1, 365)]
    public int? ExpiresInDays { get; set; }
}

/// <summary>
/// Response when creating a new API key
/// </summary>
public class CreateApiKeyResponse
{
    public string Id { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string[] Scopes { get; set; } = default!;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// API key information (without sensitive data)
/// </summary>
public class ApiKeyDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string[] Scopes { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    
    // Enhanced features
    public string RateLimitTier { get; set; } = "basic";
    public string? RotatedFromKeyId { get; set; }
    public DateTime? RotatedAt { get; set; }
    public bool IsNearExpiration { get; set; }
    public int? DaysUntilExpiration { get; set; }
}

/// <summary>
/// Response containing list of API keys
/// </summary>
public class ApiKeyListResponse
{
    public IEnumerable<ApiKeyDto> Keys { get; set; } = new List<ApiKeyDto>();
}

/// <summary>
/// Request to validate an API key
/// </summary>
public class ValidateApiKeyRequest
{
    [Required]
    public string ApiKey { get; set; } = default!;
}

/// <summary>
/// Response for API key validation
/// </summary>
public class ApiKeyValidationResponse
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string[]? Scopes { get; set; }
    public string? Error { get; set; }
    
    // Enhanced features
    public RateLimitInfo? RateLimitInfo { get; set; }
    public bool IsNearExpiration { get; set; }
    public int? DaysUntilExpiration { get; set; }
}

/// <summary>
/// Request to rotate an API key
/// </summary>
public class RotateApiKeyRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Response when rotating an API key
/// </summary>
public class RotateApiKeyResponse
{
    public string Id { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string[] Scopes { get; set; } = default!;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RotatedFromKeyId { get; set; } = default!;
}

/// <summary>
/// API key usage statistics response
/// </summary>
public class ApiKeyUsageStatsResponse
{
    public long TotalRequests { get; set; }
    public Dictionary<string, long> EndpointUsage { get; set; } = new();
    public Dictionary<int, long> StatusCodeDistribution { get; set; } = new();
    public DateTime? FirstUsed { get; set; }
    public DateTime? LastUsed { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public string RateLimitTier { get; set; } = "basic";
    public int RequestsInCurrentWindow { get; set; }
    public int RequestLimit { get; set; }
    public DateTime WindowResetsAt { get; set; }
}