using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CollaborativePuzzle.Infrastructure.Repositories;

/// <summary>
/// Minimal in-memory implementation of IApiKeyRepository for development
/// </summary>
public class MinimalApiKeyRepository : IApiKeyRepository
{
    private readonly ConcurrentDictionary<string, ApiKey> _apiKeys = new();
    private readonly ConcurrentDictionary<string, ApiKey> _apiKeysByHash = new();
    private readonly ILogger<MinimalApiKeyRepository> _logger;

    public MinimalApiKeyRepository(ILogger<MinimalApiKeyRepository> logger)
    {
        _logger = logger;
    }

    public Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey)
    {
        _apiKeys[apiKey.Id] = apiKey;
        _apiKeysByHash[apiKey.KeyHash] = apiKey;
        _logger.LogInformation("Created API key {KeyId} for user {UserId}", apiKey.Id, apiKey.UserId);
        return Task.FromResult(apiKey);
    }

    public Task<ApiKey?> GetApiKeyAsync(string keyId)
    {
        _apiKeys.TryGetValue(keyId, out var apiKey);
        return Task.FromResult(apiKey);
    }

    public Task<ApiKey?> GetApiKeyByHashAsync(string keyHash)
    {
        _apiKeysByHash.TryGetValue(keyHash, out var apiKey);
        return Task.FromResult(apiKey);
    }

    public Task<IEnumerable<ApiKey>> GetApiKeysByUserAsync(string userId)
    {
        var userKeys = _apiKeys.Values.Where(k => k.UserId == userId);
        return Task.FromResult(userKeys);
    }

    public Task<bool> UpdateApiKeyAsync(ApiKey apiKey)
    {
        if (_apiKeys.ContainsKey(apiKey.Id))
        {
            _apiKeys[apiKey.Id] = apiKey;
            _apiKeysByHash[apiKey.KeyHash] = apiKey;
            _logger.LogInformation("Updated API key {KeyId}", apiKey.Id);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task UpdateLastUsedAsync(string keyId)
    {
        if (_apiKeys.TryGetValue(keyId, out var apiKey))
        {
            apiKey.LastUsedAt = DateTime.UtcNow;
            _logger.LogDebug("Updated last used for API key {KeyId}", keyId);
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteApiKeyAsync(string keyId)
    {
        if (_apiKeys.TryRemove(keyId, out var apiKey))
        {
            _apiKeysByHash.TryRemove(apiKey.KeyHash, out _);
            _logger.LogInformation("Deleted API key {KeyId}", keyId);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // Enhanced features

    public Task<ApiKey?> GetByIdAsync(string keyId)
    {
        return GetApiKeyAsync(keyId);
    }

    public Task<ApiKey?> GetByKeyAsync(string key)
    {
        // In a real implementation, we'd hash the key and look it up
        var apiKey = _apiKeys.Values.FirstOrDefault(k => k.Key == key);
        return Task.FromResult(apiKey);
    }

    public Task<ApiKey> CreateAsync(ApiKey apiKey)
    {
        return CreateApiKeyAsync(apiKey);
    }

    public Task<ApiKey> UpdateAsync(ApiKey apiKey)
    {
        _apiKeys[apiKey.Id] = apiKey;
        if (!string.IsNullOrEmpty(apiKey.KeyHash))
        {
            _apiKeysByHash[apiKey.KeyHash] = apiKey;
        }
        return Task.FromResult(apiKey);
    }

    public Task<ApiKeyTemplate> GetTemplateAsync(string templateId)
    {
        // Predefined templates
        var templates = new Dictionary<string, ApiKeyTemplate>
        {
            ["basic-read"] = new ApiKeyTemplate
            {
                Id = "basic-read",
                Name = "Basic Read Access",
                Description = "Read-only access to puzzles and sessions",
                Scopes = new[] { ApiScopes.ReadPuzzles, ApiScopes.ReadSessions },
                RateLimitTier = "basic",
                DefaultExpiryDays = 30
            },
            ["standard-full"] = new ApiKeyTemplate
            {
                Id = "standard-full",
                Name = "Standard Full Access",
                Description = "Full access to puzzles and sessions",
                Scopes = new[] { ApiScopes.PuzzlesAll, ApiScopes.SessionsAll },
                RateLimitTier = "standard",
                DefaultExpiryDays = 90
            },
            ["admin-all"] = new ApiKeyTemplate
            {
                Id = "admin-all",
                Name = "Admin Access",
                Description = "Full administrative access",
                Scopes = new[] { ApiScopes.AdminAll },
                RateLimitTier = "premium",
                DefaultExpiryDays = 365
            }
        };

        templates.TryGetValue(templateId, out var template);
        return Task.FromResult(template ?? throw new ArgumentException($"Template {templateId} not found"));
    }

    public Task<IEnumerable<ApiKeyTemplate>> GetTemplatesAsync()
    {
        var templates = new[]
        {
            new ApiKeyTemplate
            {
                Id = "basic-read",
                Name = "Basic Read Access",
                Description = "Read-only access to puzzles and sessions",
                Scopes = new[] { ApiScopes.ReadPuzzles, ApiScopes.ReadSessions },
                RateLimitTier = "basic",
                DefaultExpiryDays = 30
            },
            new ApiKeyTemplate
            {
                Id = "standard-full",
                Name = "Standard Full Access",
                Description = "Full access to puzzles and sessions",
                Scopes = new[] { ApiScopes.PuzzlesAll, ApiScopes.SessionsAll },
                RateLimitTier = "standard",
                DefaultExpiryDays = 90
            },
            new ApiKeyTemplate
            {
                Id = "admin-all",
                Name = "Admin Access",
                Description = "Full administrative access",
                Scopes = new[] { ApiScopes.AdminAll },
                RateLimitTier = "premium",
                DefaultExpiryDays = 365
            }
        };

        return Task.FromResult(templates.AsEnumerable());
    }
}