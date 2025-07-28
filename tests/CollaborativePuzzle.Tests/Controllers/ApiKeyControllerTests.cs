using System.Security.Claims;
using CollaborativePuzzle.Api.Controllers;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Controllers;

public class ApiKeyControllerTests
{
    private readonly Mock<IApiKeyService> _mockService;
    private readonly Mock<ILogger<ApiKeyController>> _mockLogger;
    private readonly ApiKeyController _controller;
    private readonly ClaimsPrincipal _user;

    public ApiKeyControllerTests()
    {
        _mockService = new Mock<IApiKeyService>();
        _mockLogger = new Mock<ILogger<ApiKeyController>>();
        _controller = new ApiKeyController(_mockService.Object, _mockLogger.Object);
        
        // Set up authenticated user
        _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        }, "test"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = _user }
        };
    }

    [Fact]
    public async Task CreateApiKey_WithValidRequest_ReturnsCreatedKey()
    {
        // Arrange
        var request = new CreateApiKeyRequest
        {
            Name = "Test API Key",
            Scopes = new[] { "read:puzzles", "write:puzzles" },
            ExpiresInDays = 30
        };
        
        var apiKey = new ApiKey
        {
            Id = "key123",
            UserId = "user123",
            Name = request.Name,
            Key = "cp_test-key-123",
            Scopes = request.Scopes,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        
        _mockService.Setup(x => x.GenerateApiKeyAsync(
                "user123", 
                request.Name, 
                request.Scopes, 
                It.IsAny<DateTime?>()))
            .ReturnsAsync(apiKey);
        
        // Act
        var result = await _controller.CreateApiKey(request);
        
        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<CreateApiKeyResponse>(createdResult.Value);
        Assert.Equal(apiKey.Id, response.Id);
        Assert.Equal(apiKey.Key, response.Key);
        Assert.Equal(apiKey.Name, response.Name);
        Assert.Equal(apiKey.Scopes, response.Scopes);
    }

    [Fact]
    public async Task GetApiKeys_ReturnsUserKeys()
    {
        // Arrange
        var keys = new List<ApiKey>
        {
            new ApiKey { Id = "key1", Name = "Key 1", IsActive = true },
            new ApiKey { Id = "key2", Name = "Key 2", IsActive = false }
        };
        
        _mockService.Setup(x => x.GetUserApiKeysAsync("user123"))
            .ReturnsAsync(keys);
        
        // Act
        var result = await _controller.GetApiKeys();
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiKeyListResponse>(okResult.Value);
        Assert.Equal(2, response.Keys.Count());
    }

    [Fact]
    public async Task RevokeApiKey_WithValidKey_ReturnsOk()
    {
        // Arrange
        var keyId = "key123";
        _mockService.Setup(x => x.RevokeApiKeyAsync(keyId, "user123"))
            .ReturnsAsync(true);
        
        // Act
        var result = await _controller.RevokeApiKey(keyId);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        Assert.True(response.success);
        Assert.Equal("API key revoked successfully", response.message);
    }

    [Fact]
    public async Task RevokeApiKey_WithInvalidKey_ReturnsNotFound()
    {
        // Arrange
        var keyId = "invalid-key";
        _mockService.Setup(x => x.RevokeApiKeyAsync(keyId, "user123"))
            .ReturnsAsync(false);
        
        // Act
        var result = await _controller.RevokeApiKey(keyId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        dynamic response = notFoundResult.Value!;
        Assert.Equal("API key not found or unauthorized", response.error);
    }

    [Fact]
    public async Task ValidateApiKey_WithValidKey_ReturnsOk()
    {
        // Arrange
        var apiKey = "cp_valid-key";
        var validationResult = ApiKeyValidationResult.Success("user123", new[] { "read:puzzles" });
        
        _mockService.Setup(x => x.ValidateApiKeyAsync(apiKey))
            .ReturnsAsync(validationResult);
        
        // Act
        var result = await _controller.ValidateApiKey(new ValidateApiKeyRequest { ApiKey = apiKey });
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiKeyValidationResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Equal("user123", response.UserId);
        Assert.Equal(new[] { "read:puzzles" }, response.Scopes);
    }

    [Fact]
    public async Task ValidateApiKey_WithInvalidKey_ReturnsUnauthorized()
    {
        // Arrange
        var apiKey = "invalid-key";
        var validationResult = ApiKeyValidationResult.Failure("Invalid API key");
        
        _mockService.Setup(x => x.ValidateApiKeyAsync(apiKey))
            .ReturnsAsync(validationResult);
        
        // Act
        var result = await _controller.ValidateApiKey(new ValidateApiKeyRequest { ApiKey = apiKey });
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiKeyValidationResponse>(unauthorizedResult.Value);
        Assert.False(response.IsValid);
        Assert.Equal("Invalid API key", response.Error);
    }
}