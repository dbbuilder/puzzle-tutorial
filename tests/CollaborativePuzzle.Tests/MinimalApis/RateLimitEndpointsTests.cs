using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CollaborativePuzzle.Api.MinimalApis;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.MinimalApis;

public class RateLimitEndpointsTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<Program>> _loggerMock;
    private readonly ClaimsPrincipal _authenticatedUser;
    private readonly ClaimsPrincipal _adminUser;

    public RateLimitEndpointsTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<Program>>();
        
        // Create authenticated user
        var userClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Name, "testuser")
        };
        _authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "Test"));
        
        // Create admin user
        var adminClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "admin-user-id"),
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        _adminUser = new ClaimsPrincipal(new ClaimsIdentity(adminClaims, "Test"));
    }

    [Fact]
    public async Task GetRateLimitStatus_ForAuthenticatedUser_ShouldReturnStatus()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<double>()))
            .ReturnsAsync(25);

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetRateLimitStatusAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = (Task<Ok<RateLimitStatus>>)endpoint!.Invoke(null, new object[] 
        { 
            _authenticatedUser, 
            _redisServiceMock.Object, 
            _loggerMock.Object 
        })!;
        
        var result = await task;

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(100, result.Value.Limit);
        Assert.Equal(75, result.Value.Remaining);
        Assert.Equal(25, result.Value.CurrentCount);
    }

    [Fact]
    public async Task GetRateLimitHistory_ForAuthenticatedUser_ShouldReturnHistory()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync<long>(It.IsAny<string>()))
            .ReturnsAsync((long?)10);

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetRateLimitHistoryAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = (Task<Ok<RateLimitHistory>>)endpoint!.Invoke(null, new object[] 
        { 
            _authenticatedUser, 
            _redisServiceMock.Object, 
            _loggerMock.Object 
        })!;
        
        var result = await task;

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(24, result.Value.Hours.Count);
        Assert.All(result.Value.Hours, h => Assert.Equal(10, h.TotalRequests));
    }

    [Fact]
    public async Task GetRateLimitStatus_ForUnauthenticatedUser_ShouldReturnDefaultStatus()
    {
        // Arrange
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetRateLimitStatusAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = (Task<Ok<RateLimitStatus>>)endpoint!.Invoke(null, new object[] 
        { 
            anonymousUser, 
            _redisServiceMock.Object, 
            _loggerMock.Object 
        })!;
        
        var result = await task;

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(60, result.Value.Limit);
        Assert.Equal(60, result.Value.Remaining);
    }

    [Fact]
    public async Task GetUserRateLimitStatus_AsAdmin_ShouldReturnUserStatus()
    {
        // Arrange
        var userId = "target-user-id";
        _redisServiceMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<double>()))
            .ReturnsAsync(50);

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetUserRateLimitStatusAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = endpoint!.Invoke(null, new object[] 
        { 
            userId,
            _redisServiceMock.Object, 
            _loggerMock.Object 
        }) as Task<dynamic>;
        
        await task!;
        var resultProperty = task.GetType().GetProperty("Result");
        var result = resultProperty!.GetValue(task);

        // Assert
        Assert.NotNull(result);
        _loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin retrieved rate limit status")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetUserRateLimit_AsAdmin_ShouldDeleteKeys()
    {
        // Arrange
        var userId = "target-user-id";
        var keys = new[] { "ratelimit:user:target-user-id:1", "ratelimit:stats:user:target-user-id:1" };
        
        _redisServiceMock.Setup(x => x.GetKeysAsync(It.IsAny<string>()))
            .ReturnsAsync(keys);

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("ResetUserRateLimitAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = endpoint!.Invoke(null, new object[] 
        { 
            userId,
            _redisServiceMock.Object, 
            _loggerMock.Object 
        }) as Task<dynamic>;
        
        await task!;

        // Assert
        _redisServiceMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Exactly(2));
        _loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Admin reset rate limit")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRateLimitStatus_WithPremiumTier_ShouldReturn10000Limit()
    {
        // Arrange
        var premiumClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "premium-user-id"),
            new("api_tier", "premium")
        };
        var premiumUser = new ClaimsPrincipal(new ClaimsIdentity(premiumClaims, "Test"));

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetRateLimitStatusAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = (Task<Ok<RateLimitStatus>>)endpoint!.Invoke(null, new object[] 
        { 
            premiumUser, 
            _redisServiceMock.Object, 
            _loggerMock.Object 
        })!;
        
        var result = await task;

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(10000, result.Value.Limit);
    }

    [Fact]
    public async Task GetRateLimitHistory_ShouldCalculateTotals()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync<long>(It.IsRegex(".*:total")))
            .ReturnsAsync((long?)100);
        _redisServiceMock.Setup(x => x.GetAsync<long>(It.IsRegex(".*:limited")))
            .ReturnsAsync((long?)5);

        // Act
        var endpoint = typeof(RateLimitEndpoints)
            .GetMethod("GetRateLimitHistoryAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var task = (Task<Ok<RateLimitHistory>>)endpoint!.Invoke(null, new object[] 
        { 
            _authenticatedUser, 
            _redisServiceMock.Object, 
            _loggerMock.Object 
        })!;
        
        var result = await task;

        // Assert
        Assert.NotNull(result.Value);
        Assert.Equal(2400, result.Value.TotalRequests); // 24 hours * 100
        Assert.Equal(120, result.Value.TotalRateLimited); // 24 hours * 5
    }
}