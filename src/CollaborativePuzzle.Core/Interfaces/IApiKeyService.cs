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
}