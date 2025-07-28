using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Services;

/// <summary>
/// Tests for enhanced API key authentication features
/// </summary>
public class ApiKeyServiceEnhancedTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepository;
    private readonly Mock<IRedisService> _redisService;
    private readonly Mock<ILogger<ApiKeyService>> _logger;
    private readonly ApiKeyService _apiKeyService;

    public ApiKeyServiceEnhancedTests()
    {
        _apiKeyRepository = new Mock<IApiKeyRepository>();
        _redisService = new Mock<IRedisService>();
        _logger = new Mock<ILogger<ApiKeyService>>();
        
        _apiKeyService = new ApiKeyService(
            _apiKeyRepository.Object,
            _redisService.Object,
            _logger.Object);
    }

    #region API Key Rotation Tests

    [Fact]
    public async Task RotateApiKeyAsync_Should_Create_New_Key_And_Invalidate_Old()
    {
        // Arrange
        var userId = "user123";
        var oldKeyId = "key123";
        var oldApiKey = new ApiKey
        {
            Id = oldKeyId,
            UserId = userId,
            Key = "cp_old_key_123",
            Name = "Production API Key",
            Scopes = new[] { "read_puzzles", "write_puzzles" },
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _apiKeyRepository.Setup(r => r.GetByIdAsync(oldKeyId))
            .ReturnsAsync(oldApiKey);

        _apiKeyRepository.Setup(r => r.CreateAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync((ApiKey key) => key);

        // Act
        var result = await _apiKeyService.RotateApiKeyAsync(oldKeyId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(oldApiKey.Key, result.Key);
        Assert.Equal(oldApiKey.Name + " (Rotated)", result.Name);
        Assert.Equal(oldApiKey.Scopes, result.Scopes);
        Assert.True(result.IsActive);
        Assert.NotNull(result.RotatedFromKeyId);
        Assert.Equal(oldKeyId, result.RotatedFromKeyId);

        // Verify old key was revoked
        _apiKeyRepository.Verify(r => r.UpdateAsync(It.Is<ApiKey>(k => 
            k.Id == oldKeyId && 
            !k.IsActive && 
            k.RevokedAt.HasValue)), Times.Once);

        // Verify cache was invalidated
        _redisService.Verify(r => r.DeleteAsync($"apikey:{oldApiKey.Key}"), Times.Once);
    }

    [Fact]
    public async Task RotateApiKeyAsync_Should_Fail_For_Invalid_User()
    {
        // Arrange
        var keyId = "key123";
        var wrongUserId = "wronguser";
        var apiKey = new ApiKey
        {
            Id = keyId,
            UserId = "correctuser",
            Key = "cp_key_123"
        };

        _apiKeyRepository.Setup(r => r.GetByIdAsync(keyId))
            .ReturnsAsync(apiKey);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _apiKeyService.RotateApiKeyAsync(keyId, wrongUserId));
    }

    #endregion

    #region Hierarchical Scope Validation Tests

    [Theory]
    [InlineData("admin:*", new[] { "read_puzzles", "write_puzzles", "delete_puzzles" }, true)]
    [InlineData("puzzles:*", new[] { "read_puzzles", "write_puzzles" }, true)]
    [InlineData("puzzles:read", new[] { "read_puzzles" }, true)]
    [InlineData("puzzles:read", new[] { "write_puzzles" }, false)]
    [InlineData("admin:users", new[] { "admin_system" }, false)]
    public void ValidateHierarchicalScope_Should_Check_Permissions_Correctly(
        string grantedScope, string[] requiredScopes, bool expectedResult)
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Scopes = new[] { grantedScope }
        };

        // Act
        var result = _apiKeyService.ValidateHierarchicalScopes(apiKey, requiredScopes);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void ValidateHierarchicalScope_Should_Handle_Multiple_Wildcard_Scopes()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Scopes = new[] { "puzzles:*", "sessions:read" }
        };

        var requiredScopes = new[] { "read_puzzles", "write_puzzles", "read_sessions" };

        // Act
        var result = _apiKeyService.ValidateHierarchicalScopes(apiKey, requiredScopes);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region API Key Usage Analytics Tests

    [Fact]
    public async Task TrackApiKeyUsageAsync_Should_Record_Usage_Metrics()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = "key123",
            Key = "cp_key_123",
            UserId = "user123"
        };

        var endpoint = "/api/v1/puzzles";
        var statusCode = 200;
        var responseTime = 125;

        // Act
        await _apiKeyService.TrackApiKeyUsageAsync(apiKey, endpoint, statusCode, responseTime);

        // Assert
        // Verify usage count was incremented
        _redisService.Verify(r => r.IncrementAsync(
            $"apikey:usage:{apiKey.Id}:count"), Times.Once);

        // Verify endpoint-specific count was incremented
        _redisService.Verify(r => r.IncrementAsync(
            $"apikey:usage:{apiKey.Id}:endpoint:{endpoint}"), Times.Once);

        // Verify response time was tracked
        _redisService.Verify(r => r.SetObjectAsync(
            It.Is<string>(s => s.StartsWith($"apikey:usage:{apiKey.Id}:metrics:")),
            It.Is<ApiKeyUsageMetric>(m => 
                m.Endpoint == endpoint && 
                m.StatusCode == statusCode && 
                m.ResponseTimeMs == responseTime),
            It.IsAny<TimeSpan>()), Times.Once);

        // Verify last used timestamp was updated
        _apiKeyRepository.Verify(r => r.UpdateLastUsedAsync(apiKey.Id), Times.Once);
    }

    [Fact]
    public async Task GetApiKeyUsageStatsAsync_Should_Return_Aggregated_Metrics()
    {
        // Arrange
        var keyId = "key123";
        var totalCount = 1500L;
        var endpoints = new Dictionary<string, long>
        {
            { "/api/v1/puzzles", 800L },
            { "/api/v1/sessions", 500L },
            { "/api/v1/users", 200L }
        };

        _redisService.Setup(r => r.GetAsync<long>($"apikey:usage:{keyId}:count"))
            .ReturnsAsync(totalCount);

        foreach (var endpoint in endpoints)
        {
            _redisService.Setup(r => r.GetAsync<long>(
                $"apikey:usage:{keyId}:endpoint:{endpoint.Key}"))
                .ReturnsAsync(endpoint.Value);
        }

        // Act
        var result = await _apiKeyService.GetApiKeyUsageStatsAsync(keyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(totalCount, result.TotalRequests);
        Assert.Equal(3, result.EndpointUsage.Count);
        Assert.Equal(800L, result.EndpointUsage["/api/v1/puzzles"]);
    }

    #endregion

    #region Rate Limiting Integration Tests

    [Fact]
    public async Task ValidateApiKeyAsync_Should_Check_Rate_Limits()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = "key123",
            Key = "cp_key_123",
            UserId = "user123",
            Scopes = new[] { "read_puzzles" },
            IsActive = true,
            RateLimitTier = "premium" // Premium tier with higher limits
        };

        _apiKeyRepository.Setup(r => r.GetByKeyAsync(apiKey.Key))
            .ReturnsAsync(apiKey);

        // Simulate rate limit check
        _redisService.Setup(r => r.GetAsync<int>($"ratelimit:apikey:{apiKey.Id}"))
            .ReturnsAsync(95); // Under the limit

        // Act
        var result = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(apiKey.UserId, result.UserId);
        Assert.NotNull(result.RateLimitInfo);
        Assert.Equal("premium", result.RateLimitInfo.Tier);
        Assert.Equal(95, result.RateLimitInfo.CurrentUsage);
        Assert.Equal(1000, result.RateLimitInfo.Limit); // Premium limit
    }

    [Fact]
    public async Task ValidateApiKeyAsync_Should_Fail_When_Rate_Limit_Exceeded()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = "key123",
            Key = "cp_key_123",
            UserId = "user123",
            Scopes = new[] { "read_puzzles" },
            IsActive = true,
            RateLimitTier = "basic" // Basic tier with lower limits
        };

        _apiKeyRepository.Setup(r => r.GetByKeyAsync(apiKey.Key))
            .ReturnsAsync(apiKey);

        // Simulate rate limit exceeded
        _redisService.Setup(r => r.GetAsync<int>($"ratelimit:apikey:{apiKey.Id}"))
            .ReturnsAsync(105); // Over the basic limit of 100

        // Act
        var result = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Rate limit exceeded", result.Error);
        Assert.NotNull(result.RateLimitInfo);
        Assert.Equal(105, result.RateLimitInfo.CurrentUsage);
        Assert.Equal(100, result.RateLimitInfo.Limit); // Basic limit
    }

    #endregion

    #region API Key Expiration Grace Period Tests

    [Fact]
    public async Task ValidateApiKeyAsync_Should_Allow_Grace_Period_For_Expiring_Keys()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = "key123",
            Key = "cp_key_123",
            UserId = "user123",
            Scopes = new[] { "read_puzzles" },
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(3) // Expires in 3 days
        };

        _apiKeyRepository.Setup(r => r.GetByKeyAsync(apiKey.Key))
            .ReturnsAsync(apiKey);

        // Act
        var result = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsNearExpiration);
        Assert.Equal(3, result.DaysUntilExpiration);
    }

    #endregion

    #region Scope Inheritance Tests

    [Fact]
    public async Task CreateApiKeyWithTemplateAsync_Should_Inherit_Scopes_From_Template()
    {
        // Arrange
        var userId = "user123";
        var templateId = "template123";
        var template = new ApiKeyTemplate
        {
            Id = templateId,
            Name = "Standard Read Access",
            Scopes = new[] { "read_puzzles", "read_sessions", "read_users" },
            RateLimitTier = "standard",
            DefaultExpiryDays = 90
        };

        _apiKeyRepository.Setup(r => r.GetTemplateAsync(templateId))
            .ReturnsAsync(template);

        _apiKeyRepository.Setup(r => r.CreateAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync((ApiKey key) => key);

        // Act
        var result = await _apiKeyService.CreateApiKeyFromTemplateAsync(
            userId, "My API Key", templateId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(template.Scopes, result.Scopes);
        Assert.Equal(template.RateLimitTier, result.RateLimitTier);
        Assert.Equal(DateTime.UtcNow.AddDays(90).Date, result.ExpiresAt?.Date);
    }

    #endregion
}

/// <summary>
/// Test models for enhanced features
/// </summary>
public class ApiKeyUsageMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ApiKeyTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public string RateLimitTier { get; set; } = "basic";
    public int DefaultExpiryDays { get; set; } = 30;
}