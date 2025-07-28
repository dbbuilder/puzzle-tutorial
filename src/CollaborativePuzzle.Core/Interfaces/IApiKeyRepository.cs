using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces;

/// <summary>
/// Repository interface for API key data access
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Creates a new API key
    /// </summary>
    Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey);
    
    /// <summary>
    /// Gets an API key by ID
    /// </summary>
    Task<ApiKey?> GetApiKeyAsync(string keyId);
    
    /// <summary>
    /// Gets an API key by its hash
    /// </summary>
    Task<ApiKey?> GetApiKeyByHashAsync(string keyHash);
    
    /// <summary>
    /// Gets all API keys for a user
    /// </summary>
    Task<IEnumerable<ApiKey>> GetApiKeysByUserAsync(string userId);
    
    /// <summary>
    /// Updates an API key
    /// </summary>
    Task<bool> UpdateApiKeyAsync(ApiKey apiKey);
    
    /// <summary>
    /// Updates the last used timestamp for an API key
    /// </summary>
    Task UpdateLastUsedAsync(string keyId);
    
    /// <summary>
    /// Deletes an API key
    /// </summary>
    Task<bool> DeleteApiKeyAsync(string keyId);
}