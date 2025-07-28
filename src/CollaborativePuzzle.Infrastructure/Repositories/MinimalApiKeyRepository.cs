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
}