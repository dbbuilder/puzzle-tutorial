using System.Security.Cryptography;
using System.Text;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.Extensions.Logging;

namespace CollaborativePuzzle.Infrastructure.Services;

/// <summary>
/// Service implementation for managing API keys
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _repository;
    private readonly ILogger<IApiKeyService> _logger;
    private const int KeyLength = 32; // 256 bits
    private const string KeyPrefix = "cp_"; // CollaborativePuzzle prefix

    public ApiKeyService(IApiKeyRepository repository, ILogger<IApiKeyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ApiKey> GenerateApiKeyAsync(string userId, string name, string[] scopes, DateTime? expiresAt)
    {
        // Generate a cryptographically secure random key
        var keyBytes = new byte[KeyLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        
        // Create a URL-safe base64 encoded key with prefix
        var key = KeyPrefix + Convert.ToBase64String(keyBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        // Hash the key for storage
        var keyHash = HashApiKey(key);
        
        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = name,
            Key = key, // Only returned when creating
            KeyHash = keyHash,
            Scopes = scopes,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        var created = await _repository.CreateApiKeyAsync(apiKey);
        _logger.LogInformation("API key created for user {UserId} with scopes: {Scopes}", 
            userId, string.Join(", ", scopes));
        
        return created;
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(KeyPrefix))
        {
            return ApiKeyValidationResult.Failure("Invalid API key format");
        }
        
        var keyHash = HashApiKey(apiKey);
        var storedKey = await _repository.GetApiKeyByHashAsync(keyHash);
        
        if (storedKey == null)
        {
            _logger.LogWarning("Invalid API key attempted");
            return ApiKeyValidationResult.Failure("Invalid API key");
        }
        
        if (!storedKey.IsActive)
        {
            _logger.LogWarning("Inactive API key used: {KeyId}", storedKey.Id);
            return ApiKeyValidationResult.Failure("API key is inactive");
        }
        
        if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key used: {KeyId}", storedKey.Id);
            return ApiKeyValidationResult.Failure("API key has expired");
        }
        
        // Update last used timestamp asynchronously (fire and forget)
        _ = Task.Run(async () => 
        {
            try
            {
                await _repository.UpdateLastUsedAsync(storedKey.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update last used timestamp for key {KeyId}", storedKey.Id);
            }
        });
        
        return ApiKeyValidationResult.Success(storedKey.UserId, storedKey.Scopes);
    }

    public async Task<bool> RevokeApiKeyAsync(string keyId, string userId)
    {
        var apiKey = await _repository.GetApiKeyAsync(keyId);
        if (apiKey == null || apiKey.UserId != userId)
        {
            _logger.LogWarning("Attempt to revoke API key {KeyId} by unauthorized user {UserId}", 
                keyId, userId);
            return false;
        }
        
        apiKey.IsActive = false;
        apiKey.RevokedAt = DateTime.UtcNow;
        
        var result = await _repository.UpdateApiKeyAsync(apiKey);
        if (result)
        {
            _logger.LogInformation("API key {KeyId} revoked by user {UserId}", keyId, userId);
        }
        
        return result;
    }

    public async Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId)
    {
        var keys = await _repository.GetApiKeysByUserAsync(userId);
        
        // Remove sensitive data before returning
        return keys.Select(k => new ApiKey
        {
            Id = k.Id,
            UserId = k.UserId,
            Name = k.Name,
            Scopes = k.Scopes,
            IsActive = k.IsActive,
            ExpiresAt = k.ExpiresAt,
            CreatedAt = k.CreatedAt,
            LastUsedAt = k.LastUsedAt,
            RevokedAt = k.RevokedAt
            // Key and KeyHash are not included
        });
    }

    public async Task<bool> HasScopeAsync(string keyId, string scope)
    {
        var apiKey = await _repository.GetApiKeyAsync(keyId);
        return apiKey?.Scopes?.Contains(scope) ?? false;
    }

    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}