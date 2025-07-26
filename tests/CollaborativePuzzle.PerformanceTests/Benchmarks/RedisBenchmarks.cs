using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using StackExchange.Redis;
using System.Text.Json;

namespace CollaborativePuzzle.PerformanceTests.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RedisBenchmarks
{
    private IConnectionMultiplexer _redis = null!;
    private IDatabase _db = null!;
    private readonly Random _random = new();

    [GlobalSetup]
    public void Setup()
    {
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
        _db = _redis.GetDatabase();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _redis?.Dispose();
    }

    [Benchmark]
    public async Task<string?> SimpleGet()
    {
        return await _db.StringGetAsync("benchmark:simple");
    }

    [Benchmark]
    public async Task SimpleSet()
    {
        await _db.StringSetAsync($"benchmark:set:{_random.Next(1000)}", "test-value", TimeSpan.FromMinutes(1));
    }

    [Benchmark]
    public async Task<bool> DistributedLock()
    {
        var lockKey = $"lock:benchmark:{_random.Next(100)}";
        return await _db.LockTakeAsync(lockKey, "owner", TimeSpan.FromSeconds(5));
    }

    [Benchmark]
    public async Task PubSub()
    {
        var channel = new RedisChannel("benchmark:channel", RedisChannel.PatternMode.Literal);
        await _redis.GetSubscriber().PublishAsync(channel, "test-message");
    }

    [Benchmark]
    public async Task JsonSerializationRoundtrip()
    {
        var data = new PuzzlePieceData
        {
            PieceId = Guid.NewGuid().ToString(),
            Position = new Position { X = _random.Next(1000), Y = _random.Next(1000) },
            Rotation = _random.Next(360),
            IsLocked = _random.Next(2) == 1
        };

        var json = JsonSerializer.Serialize(data);
        await _db.StringSetAsync($"benchmark:json:{_random.Next(1000)}", json);
        
        var retrieved = await _db.StringGetAsync($"benchmark:json:{_random.Next(1000)}");
        if (retrieved.HasValue)
        {
            return JsonSerializer.Deserialize<PuzzlePieceData>(retrieved!);
        }
        return null;
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task BatchOperations(int batchSize)
    {
        var batch = _db.CreateBatch();
        var tasks = new List<Task>();

        for (int i = 0; i < batchSize; i++)
        {
            tasks.Add(batch.StringSetAsync($"benchmark:batch:{i}", $"value-{i}"));
        }

        batch.Execute();
        await Task.WhenAll(tasks);
    }
}

public class PuzzlePieceData
{
    public string PieceId { get; set; } = string.Empty;
    public Position Position { get; set; } = new();
    public float Rotation { get; set; }
    public bool IsLocked { get; set; }
}

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}