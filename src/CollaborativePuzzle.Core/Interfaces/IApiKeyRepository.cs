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
    
    // Enhanced features
    
    /// <summary>
    /// Gets an API key by ID (includes userId check)
    /// </summary>
    Task<ApiKey?> GetByIdAsync(string keyId);
    
    /// <summary>
    /// Gets an API key by the actual key value
    /// </summary>
    Task<ApiKey?> GetByKeyAsync(string key);
    
    /// <summary>
    /// Creates a new API key in the repository
    /// </summary>
    Task<ApiKey> CreateAsync(ApiKey apiKey);
    
    /// <summary>
    /// Updates an existing API key
    /// </summary>
    Task<ApiKey> UpdateAsync(ApiKey apiKey);
    
    /// <summary>
    /// Gets an API key template
    /// </summary>
    Task<ApiKeyTemplate> GetTemplateAsync(string templateId);
    
    /// <summary>
    /// Gets all available API key templates
    /// </summary>
    Task<IEnumerable<ApiKeyTemplate>> GetTemplatesAsync();
}

/// <summary>
/// API key template for creating standardized keys
/// </summary>
public class ApiKeyTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public string RateLimitTier { get; set; } = "basic";
    public int DefaultExpiryDays { get; set; } = 30;
    public Dictionary<string, object>? DefaultMetadata { get; set; }
}