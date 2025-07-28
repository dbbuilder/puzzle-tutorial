using System;
using System.Threading.Tasks;
using CollaborativePuzzle.Api.RateLimiting;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CollaborativePuzzle.Tests.RateLimiting;

public class RedisRateLimitStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisRateLimitStore>> _loggerMock;
    private readonly RedisRateLimitStore _store;

    public RedisRateLimitStoreTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisRateLimitStore>>();
        
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
        
        _store = new RedisRateLimitStore(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenUnderLimit_ShouldReturnAcquiredLease()
    {
        // Arrange
        var partitionKey = "user:123";
        var permitCount = 1;
        var window = TimeSpan.FromMinutes(1);
        var limit = 10;
        
        _databaseMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        // Act
        var result = await _store.TryAcquireAsync(partitionKey, permitCount, window, limit);

        // Assert
        Assert.True(result.IsAcquired);
        Assert.Null(result.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAtLimit_ShouldReturnDeniedLease()
    {
        // Arrange
        var partitionKey = "user:123";
        var permitCount = 1;
        var window = TimeSpan.FromMinutes(1);
        var limit = 10;
        
        _databaseMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(10);

        var entries = new[] { new SortedSetEntry("req1", 100) };
        _databaseMock.Setup(x => x.SortedSetRangeByScoreWithScoresAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<Order>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _store.TryAcquireAsync(partitionKey, permitCount, window, limit);

        // Assert
        Assert.False(result.IsAcquired);
        Assert.NotNull(result.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisError_ShouldFailOpen()
    {
        // Arrange
        var partitionKey = "user:123";
        var permitCount = 1;
        var window = TimeSpan.FromMinutes(1);
        var limit = 10;
        
        _databaseMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act
        var result = await _store.TryAcquireAsync(partitionKey, permitCount, window, limit);

        // Assert
        Assert.True(result.IsAcquired); // Fail open
        _loggerMock.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCurrentCount()
    {
        // Arrange
        var partitionKey = "user:123";
        var window = TimeSpan.FromMinutes(1);
        
        _databaseMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(7);

        // Act
        var result = await _store.GetStatisticsAsync(partitionKey, window);

        // Assert
        Assert.Equal(7, result.CurrentCount);
        Assert.NotEqual(default, result.WindowStart);
        Assert.NotEqual(default, result.WindowEnd);
    }

    [Fact]
    public async Task TryAcquireAsync_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var partitionKey = "user:123";
        var permitCount = 1;
        var window = TimeSpan.FromMinutes(1);
        var limit = 10;

        // Act
        await _store.TryAcquireAsync(partitionKey, permitCount, window, limit);

        // Assert
        _databaseMock.Verify(x => x.SortedSetRemoveRangeByScoreAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<double>(),
            It.IsAny<double>(),
            It.IsAny<Exclude>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_ShouldSetKeyExpiry()
    {
        // Arrange
        var partitionKey = "user:123";
        var permitCount = 1;
        var window = TimeSpan.FromMinutes(1);
        var limit = 10;
        
        _databaseMock.Setup(x => x.SortedSetLengthAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<Exclude>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        // Act
        await _store.TryAcquireAsync(partitionKey, permitCount, window, limit);

        // Assert
        _databaseMock.Verify(x => x.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()),
            Times.Once);
    }
}