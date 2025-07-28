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
    private readonly IRedisService _redisService;
    private readonly ILogger<IApiKeyService> _logger;
    private const int KeyLength = 32; // 256 bits
    private const string KeyPrefix = "cp_"; // CollaborativePuzzle prefix
    
    // Rate limit tiers
    private static readonly Dictionary<string, (int minute, int hour, int day)> RateLimitTiers = new()
    {
        ["basic"] = (10, 100, 1000),
        ["standard"] = (30, 500, 5000),
        ["premium"] = (100, 1000, 10000),
        ["unlimited"] = (int.MaxValue, int.MaxValue, int.MaxValue)
    };

    public ApiKeyService(IApiKeyRepository repository, IRedisService redisService, ILogger<IApiKeyService> logger)
    {
        _repository = repository;
        _redisService = redisService;
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
        try
        {
            // Check cache first
            var cachedValidation = await _redisService.GetAsync<ApiKeyValidationResult>($"apikey:{apiKey}");
            if (cachedValidation != null)
            {
                _logger.LogDebug("API key validation found in cache");
                return cachedValidation;
            }
            
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
            
            // Check rate limits
            var rateLimitInfo = await CheckRateLimitAsync(storedKey);
            if (rateLimitInfo != null && rateLimitInfo.CurrentUsage >= rateLimitInfo.Limit)
            {
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    Error = "Rate limit exceeded",
                    RateLimitInfo = rateLimitInfo
                };
            }
            
            // Check near expiration
            var daysUntilExpiration = storedKey.ExpiresAt.HasValue 
                ? (int)(storedKey.ExpiresAt.Value - DateTime.UtcNow).TotalDays 
                : (int?)null;
            
            var result = new ApiKeyValidationResult
            {
                IsValid = true,
                UserId = storedKey.UserId,
                ApiKeyId = storedKey.Id,
                Scopes = storedKey.Scopes,
                RateLimitInfo = rateLimitInfo,
                IsNearExpiration = daysUntilExpiration.HasValue && daysUntilExpiration.Value <= 7,
                DaysUntilExpiration = daysUntilExpiration
            };
            
            // Cache the result for 5 minutes
            await _redisService.SetAsync($"apikey:{apiKey}", result, TimeSpan.FromMinutes(5));
            
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
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return ApiKeyValidationResult.Failure("Error validating API key");
        }
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

    // Enhanced features
    
    public async Task<ApiKey> RotateApiKeyAsync(string keyId, string userId)
    {
        var oldKey = await _repository.GetByIdAsync(keyId);
        if (oldKey == null || oldKey.UserId != userId)
        {
            throw new UnauthorizedAccessException("API key not found or unauthorized");
        }
        
        // Create new key with same permissions
        var newKey = await GenerateApiKeyAsync(
            userId,
            oldKey.Name + " (Rotated)",
            oldKey.Scopes,
            oldKey.ExpiresAt);
        
        // Link to old key
        newKey.RotatedFromKeyId = oldKey.Id;
        newKey.RotatedAt = DateTime.UtcNow;
        newKey.RateLimitTier = oldKey.RateLimitTier;
        newKey.Metadata = oldKey.Metadata;
        
        await _repository.UpdateAsync(newKey);
        
        // Revoke old key
        await RevokeApiKeyAsync(keyId, userId);
        
        // Invalidate cache
        await _redisService.DeleteAsync($"apikey:{oldKey.Key}");
        
        _logger.LogInformation("Rotated API key {OldKeyId} to {NewKeyId} for user {UserId}", 
            keyId, newKey.Id, userId);
        
        return newKey;
    }
    
    public bool ValidateHierarchicalScopes(ApiKey apiKey, string[] requiredScopes)
    {
        var grantedScopes = new HashSet<string>();
        
        // Expand hierarchical scopes
        foreach (var scope in apiKey.Scopes)
        {
            grantedScopes.Add(scope);
            
            // Check if it's a wildcard scope
            if (ApiScopes.ScopeHierarchy.TryGetValue(scope, out var expandedScopes))
            {
                foreach (var expanded in expandedScopes)
                {
                    grantedScopes.Add(expanded);
                }
            }
        }
        
        // Check if all required scopes are satisfied
        return requiredScopes.All(required => grantedScopes.Contains(required));
    }
    
    public async Task TrackApiKeyUsageAsync(ApiKey apiKey, string endpoint, int statusCode, int responseTimeMs)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var metric = new ApiKeyUsageMetric
            {
                Endpoint = endpoint,
                StatusCode = statusCode,
                ResponseTimeMs = responseTimeMs,
                Timestamp = timestamp
            };
            
            // Increment counters
            await _redisService.IncrementAsync($"apikey:usage:{apiKey.Id}:count");
            await _redisService.IncrementAsync($"apikey:usage:{apiKey.Id}:endpoint:{endpoint}");
            
            // Store detailed metric (keep last 1000)
            var metricKey = $"apikey:usage:{apiKey.Id}:metrics:{timestamp.Ticks}";
            await _redisService.SetObjectAsync(metricKey, metric, TimeSpan.FromDays(7));
            
            // Update last used
            _ = Task.Run(() => _repository.UpdateLastUsedAsync(apiKey.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking API key usage for key {KeyId}", apiKey.Id);
        }
    }
    
    public async Task<ApiKeyUsageStats> GetApiKeyUsageStatsAsync(string keyId)
    {
        var stats = new ApiKeyUsageStats();
        
        // Get total count
        var totalCountStr = await _redisService.GetStringAsync($"apikey:usage:{keyId}:count");
        stats.TotalRequests = long.TryParse(totalCountStr, out var count) ? count : 0;
        
        // Get endpoint usage
        var endpointPattern = $"apikey:usage:{keyId}:endpoint:*";
        var endpointKeys = await _redisService.GetKeysAsync(endpointPattern);
        
        foreach (var key in endpointKeys)
        {
            var endpoint = key.Substring(key.LastIndexOf(':') + 1);
            var countStr = await _redisService.GetStringAsync(key);
            if (long.TryParse(countStr, out var endpointCount))
            {
                stats.EndpointUsage[endpoint] = endpointCount;
            }
        }
        
        return stats;
    }
    
    public async Task<ApiKey> CreateApiKeyFromTemplateAsync(string userId, string name, string templateId)
    {
        var template = await _repository.GetTemplateAsync(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template {templateId} not found");
        }
        
        var expiresAt = DateTime.UtcNow.AddDays(template.DefaultExpiryDays);
        
        var apiKey = await GenerateApiKeyAsync(userId, name, template.Scopes, expiresAt);
        
        apiKey.RateLimitTier = template.RateLimitTier;
        apiKey.Metadata = template.DefaultMetadata;
        
        await _repository.UpdateAsync(apiKey);
        
        _logger.LogInformation("Created API key {KeyId} from template {TemplateId} for user {UserId}", 
            apiKey.Id, templateId, userId);
        
        return apiKey;
    }
    
    private async Task<RateLimitInfo?> CheckRateLimitAsync(ApiKey apiKey)
    {
        if (!RateLimitTiers.TryGetValue(apiKey.RateLimitTier, out var limits))
        {
            limits = RateLimitTiers["basic"];
        }
        
        // Use custom limits if specified
        var minuteLimit = apiKey.MaxRequestsPerMinute ?? limits.minute;
        
        var currentMinute = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var rateLimitKey = $"ratelimit:apikey:{apiKey.Id}:{currentMinute}";
        
        var currentUsageStr = await _redisService.GetStringAsync(rateLimitKey);
        var currentUsage = int.TryParse(currentUsageStr, out var usage) ? usage : 0;
        
        return new RateLimitInfo
        {
            Tier = apiKey.RateLimitTier,
            Limit = minuteLimit,
            CurrentUsage = currentUsage,
            Window = TimeSpan.FromMinutes(1),
            ResetsAt = DateTime.UtcNow.AddMinutes(1).AddSeconds(-DateTime.UtcNow.Second)
        };
    }

    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// API key usage metric
/// </summary>
public class ApiKeyUsageMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}