using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Services;

public class ApiKeyServiceTests
{
    private readonly Mock<IApiKeyRepository> _mockRepository;
    private readonly Mock<ILogger<IApiKeyService>> _mockLogger;
    private readonly IApiKeyService _service;

    public ApiKeyServiceTests()
    {
        _mockRepository = new Mock<IApiKeyRepository>();
        _mockLogger = new Mock<ILogger<IApiKeyService>>();
        
        // Note: We'll create the concrete implementation after writing tests
        _service = new ApiKeyService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_ForValidUser_ReturnsNewApiKey()
    {
        // Arrange
        var userId = "user123";
        var name = "My API Key";
        var scopes = new[] { "read:puzzles", "write:puzzles" };
        var expiresAt = DateTime.UtcNow.AddDays(30);
        
        _mockRepository.Setup(x => x.CreateApiKeyAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync((ApiKey key) => key);
        
        // Act
        var result = await _service.GenerateApiKeyAsync(userId, name, scopes, expiresAt);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(name, result.Name);
        Assert.Equal(scopes, result.Scopes);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.NotNull(result.Key);
        Assert.NotNull(result.KeyHash);
        Assert.True(result.IsActive);
        
        _mockRepository.Verify(x => x.CreateApiKeyAsync(It.IsAny<ApiKey>()), Times.Once);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_GeneratesUniqueKeys()
    {
        // Arrange
        var userId = "user123";
        _mockRepository.Setup(x => x.CreateApiKeyAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync((ApiKey key) => key);
        
        // Act
        var key1 = await _service.GenerateApiKeyAsync(userId, "Key 1", new[] { "read" }, null);
        var key2 = await _service.GenerateApiKeyAsync(userId, "Key 2", new[] { "read" }, null);
        
        // Assert
        Assert.NotEqual(key1.Key, key2.Key);
        Assert.NotEqual(key1.KeyHash, key2.KeyHash);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsApiKeyDetails()
    {
        // Arrange
        var apiKey = "test-api-key-123";
        var storedKey = new ApiKey
        {
            Id = "key123",
            UserId = "user123",
            Name = "Test Key",
            KeyHash = "hashed-key", // In reality, this would be a proper hash
            Scopes = new[] { "read:puzzles" },
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastUsedAt = DateTime.UtcNow.AddDays(-1)
        };
        
        _mockRepository.Setup(x => x.GetApiKeyByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.ValidateApiKeyAsync(apiKey);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(storedKey.UserId, result.UserId);
        Assert.Equal(storedKey.Scopes, result.Scopes);
        Assert.Null(result.Error);
        
        _mockRepository.Verify(x => x.UpdateLastUsedAsync(storedKey.Id), Times.Once);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsInvalid()
    {
        // Arrange
        var apiKey = "invalid-key";
        _mockRepository.Setup(x => x.GetApiKeyByHashAsync(It.IsAny<string>()))
            .ReturnsAsync((ApiKey?)null);
        
        // Act
        var result = await _service.ValidateApiKeyAsync(apiKey);
        
        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("Invalid API key", result.Error);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ReturnsInvalid()
    {
        // Arrange
        var apiKey = "expired-key";
        var storedKey = new ApiKey
        {
            Id = "key123",
            KeyHash = "hashed-key",
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };
        
        _mockRepository.Setup(x => x.GetApiKeyByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.ValidateApiKeyAsync(apiKey);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("API key has expired", result.Error);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInactiveKey_ReturnsInvalid()
    {
        // Arrange
        var apiKey = "inactive-key";
        var storedKey = new ApiKey
        {
            Id = "key123",
            KeyHash = "hashed-key",
            IsActive = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        
        _mockRepository.Setup(x => x.GetApiKeyByHashAsync(It.IsAny<string>()))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.ValidateApiKeyAsync(apiKey);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("API key is inactive", result.Error);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var keyId = "key123";
        var userId = "user123";
        var storedKey = new ApiKey
        {
            Id = keyId,
            UserId = userId,
            IsActive = true
        };
        
        _mockRepository.Setup(x => x.GetApiKeyAsync(keyId))
            .ReturnsAsync(storedKey);
        _mockRepository.Setup(x => x.UpdateApiKeyAsync(It.IsAny<ApiKey>()))
            .ReturnsAsync(true);
        
        // Act
        var result = await _service.RevokeApiKeyAsync(keyId, userId);
        
        // Assert
        Assert.True(result);
        _mockRepository.Verify(x => x.UpdateApiKeyAsync(
            It.Is<ApiKey>(k => k.Id == keyId && !k.IsActive)), Times.Once);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithWrongUser_ReturnsFalse()
    {
        // Arrange
        var keyId = "key123";
        var storedKey = new ApiKey
        {
            Id = keyId,
            UserId = "user123",
            IsActive = true
        };
        
        _mockRepository.Setup(x => x.GetApiKeyAsync(keyId))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.RevokeApiKeyAsync(keyId, "different-user");
        
        // Assert
        Assert.False(result);
        _mockRepository.Verify(x => x.UpdateApiKeyAsync(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task GetUserApiKeysAsync_ReturnsAllUserKeys()
    {
        // Arrange
        var userId = "user123";
        var keys = new List<ApiKey>
        {
            new ApiKey { Id = "key1", UserId = userId, Name = "Key 1" },
            new ApiKey { Id = "key2", UserId = userId, Name = "Key 2" }
        };
        
        _mockRepository.Setup(x => x.GetApiKeysByUserAsync(userId))
            .ReturnsAsync(keys);
        
        // Act
        var result = await _service.GetUserApiKeysAsync(userId);
        
        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, k => Assert.Null(k.Key)); // Key should not be returned
        Assert.All(result, k => Assert.Null(k.KeyHash)); // Hash should not be returned
    }

    [Fact]
    public async Task HasScopeAsync_WithValidScope_ReturnsTrue()
    {
        // Arrange
        var keyId = "key123";
        var storedKey = new ApiKey
        {
            Id = keyId,
            Scopes = new[] { "read:puzzles", "write:puzzles", "admin:users" }
        };
        
        _mockRepository.Setup(x => x.GetApiKeyAsync(keyId))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.HasScopeAsync(keyId, "write:puzzles");
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasScopeAsync_WithInvalidScope_ReturnsFalse()
    {
        // Arrange
        var keyId = "key123";
        var storedKey = new ApiKey
        {
            Id = keyId,
            Scopes = new[] { "read:puzzles" }
        };
        
        _mockRepository.Setup(x => x.GetApiKeyAsync(keyId))
            .ReturnsAsync(storedKey);
        
        // Act
        var result = await _service.HasScopeAsync(keyId, "write:puzzles");
        
        // Assert
        Assert.False(result);
    }
}