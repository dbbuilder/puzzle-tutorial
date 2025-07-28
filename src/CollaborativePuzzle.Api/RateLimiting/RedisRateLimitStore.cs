using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace CollaborativePuzzle.Api.RateLimiting;

/// <summary>
/// Redis-based distributed rate limit store for scalable rate limiting
/// </summary>
public class RedisRateLimitStore : IRateLimitStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitStore> _logger;
    private const string KeyPrefix = "ratelimit:";

    public RedisRateLimitStore(
        IConnectionMultiplexer redis,
        ILogger<RedisRateLimitStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<RateLimitLease> TryAcquireAsync(
        string partitionKey,
        int permitCount,
        TimeSpan window,
        int limit)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{partitionKey}";
            var now = DateTimeOffset.UtcNow;
            var windowStart = now.ToUnixTimeMilliseconds();
            var windowEnd = windowStart + (long)window.TotalMilliseconds;

            // Use Redis sorted set to track requests within the window
            // Score is the timestamp, member is a unique request ID
            var requestId = Guid.NewGuid().ToString();
            
            // Remove expired entries
            await db.SortedSetRemoveRangeByScoreAsync(
                key,
                0,
                windowStart - (long)window.TotalMilliseconds);

            // Count current requests in the window
            var currentCount = await db.SortedSetLengthAsync(
                key,
                windowStart - (long)window.TotalMilliseconds,
                windowStart);

            if (currentCount + permitCount > limit)
            {
                // Rate limit exceeded
                var oldestEntry = await db.SortedSetRangeByScoreWithScoresAsync(
                    key,
                    start: windowStart - (long)window.TotalMilliseconds,
                    stop: double.PositiveInfinity,
                    take: 1);

                if (oldestEntry.Length > 0)
                {
                    var retryAfter = TimeSpan.FromMilliseconds(
                        oldestEntry[0].Score + window.TotalMilliseconds - windowStart);
                    
                    return new RateLimitLease(false, null, retryAfter);
                }

                return new RateLimitLease(false, null, window);
            }

            // Add the new request(s)
            var entries = new SortedSetEntry[permitCount];
            for (int i = 0; i < permitCount; i++)
            {
                entries[i] = new SortedSetEntry(
                    $"{requestId}:{i}",
                    windowStart);
            }

            await db.SortedSetAddAsync(key, entries);
            
            // Set expiry on the key
            await db.KeyExpireAsync(key, window.Add(TimeSpan.FromMinutes(1)));

            _logger.LogDebug(
                "Rate limit acquired for {PartitionKey}: {CurrentCount}/{Limit}",
                partitionKey,
                currentCount + permitCount,
                limit);

            return new RateLimitLease(true, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring rate limit for {PartitionKey}", partitionKey);
            // Fail open - allow the request if Redis is down
            return new RateLimitLease(true, null, null);
        }
    }

    public async Task<RateLimitStatistics> GetStatisticsAsync(string partitionKey, TimeSpan window)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = $"{KeyPrefix}{partitionKey}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var currentCount = await db.SortedSetLengthAsync(
                key,
                now - (long)window.TotalMilliseconds,
                now);

            return new RateLimitStatistics
            {
                CurrentCount = (int)currentCount,
                WindowStart = DateTimeOffset.FromUnixTimeMilliseconds(now - (long)window.TotalMilliseconds),
                WindowEnd = DateTimeOffset.FromUnixTimeMilliseconds(now)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit statistics for {PartitionKey}", partitionKey);
            return new RateLimitStatistics
            {
                CurrentCount = 0,
                WindowStart = DateTimeOffset.UtcNow.Subtract(window),
                WindowEnd = DateTimeOffset.UtcNow
            };
        }
    }
}

/// <summary>
/// Interface for rate limit stores
/// </summary>
public interface IRateLimitStore
{
    Task<RateLimitLease> TryAcquireAsync(
        string partitionKey,
        int permitCount,
        TimeSpan window,
        int limit);

    Task<RateLimitStatistics> GetStatisticsAsync(
        string partitionKey,
        TimeSpan window);
}

/// <summary>
/// Rate limit lease result
/// </summary>
public class RateLimitLease
{
    public bool IsAcquired { get; }
    public string? Reason { get; }
    public TimeSpan? RetryAfter { get; }

    public RateLimitLease(bool isAcquired, string? reason, TimeSpan? retryAfter)
    {
        IsAcquired = isAcquired;
        Reason = reason;
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Rate limit statistics
/// </summary>
public class RateLimitStatistics
{
    public int CurrentCount { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
}