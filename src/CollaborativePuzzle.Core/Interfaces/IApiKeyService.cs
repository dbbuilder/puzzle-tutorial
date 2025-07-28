using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces;

/// <summary>
/// Service for managing API keys
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="name">Friendly name for the API key</param>
    /// <param name="scopes">Scopes/permissions for the key</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <returns>The created API key with the key value (only returned once)</returns>
    Task<ApiKey> GenerateApiKeyAsync(string userId, string name, string[] scopes, DateTime? expiresAt);
    
    /// <summary>
    /// Validates an API key and returns validation result
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>Validation result with user ID and scopes if valid</returns>
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Revokes an API key
    /// </summary>
    /// <param name="keyId">API key ID</param>
    /// <param name="userId">User ID (for authorization)</param>
    /// <returns>True if revoked successfully</returns>
    Task<bool> RevokeApiKeyAsync(string keyId, string userId);
    
    /// <summary>
    /// Gets all API keys for a user (without the actual key values)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of API keys</returns>
    Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId);
    
    /// <summary>
    /// Checks if an API key has a specific scope
    /// </summary>
    /// <param name="keyId">API key ID</param>
    /// <param name="scope">Scope to check</param>
    /// <returns>True if the key has the scope</returns>
    Task<bool> HasScopeAsync(string keyId, string scope);
    
    // Enhanced features
    
    /// <summary>
    /// Rotates an API key, creating a new key with the same permissions
    /// </summary>
    /// <param name="keyId">Current API key ID</param>
    /// <param name="userId">User ID (for authorization)</param>
    /// <returns>New API key with the key value</returns>
    Task<ApiKey> RotateApiKeyAsync(string keyId, string userId);
    
    /// <summary>
    /// Validates hierarchical scopes (e.g., admin:* includes admin:users)
    /// </summary>
    /// <param name="apiKey">API key to validate</param>
    /// <param name="requiredScopes">Required scopes</param>
    /// <returns>True if all required scopes are satisfied</returns>
    bool ValidateHierarchicalScopes(ApiKey apiKey, string[] requiredScopes);
    
    /// <summary>
    /// Tracks API key usage for analytics
    /// </summary>
    /// <param name="apiKey">API key used</param>
    /// <param name="endpoint">Endpoint accessed</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="responseTimeMs">Response time in milliseconds</param>
    Task TrackApiKeyUsageAsync(ApiKey apiKey, string endpoint, int statusCode, int responseTimeMs);
    
    /// <summary>
    /// Gets usage statistics for an API key
    /// </summary>
    /// <param name="keyId">API key ID</param>
    /// <returns>Usage statistics</returns>
    Task<ApiKeyUsageStats> GetApiKeyUsageStatsAsync(string keyId);
    
    /// <summary>
    /// Creates an API key from a template
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="name">Name for the API key</param>
    /// <param name="templateId">Template ID</param>
    /// <returns>New API key</returns>
    Task<ApiKey> CreateApiKeyFromTemplateAsync(string userId, string name, string templateId);
}

/// <summary>
/// API key usage statistics
/// </summary>
public class ApiKeyUsageStats
{
    public long TotalRequests { get; set; }
    public Dictionary<string, long> EndpointUsage { get; set; } = new();
    public Dictionary<int, long> StatusCodeDistribution { get; set; } = new();
    public DateTime FirstUsed { get; set; }
    public DateTime LastUsed { get; set; }
    public double AverageResponseTimeMs { get; set; }
}