using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace CollaborativePuzzle.PerformanceTests.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SignalRBenchmarks
{
    private HubConnection _hubConnection = null!;
    private readonly List<HubConnection> _connections = new();
    private readonly string _baseUrl = "http://localhost:5000/puzzlehub";

    [GlobalSetup]
    public async Task Setup()
    {
        // Create primary connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_baseUrl)
            .Build();

        await _hubConnection.StartAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        foreach (var conn in _connections)
        {
            await conn.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task SendMessage()
    {
        await _hubConnection.InvokeAsync("SendMessage", "benchmark-session", "test-message");
    }

    [Benchmark]
    public async Task MovePiece()
    {
        var command = new
        {
            SessionId = "benchmark-session",
            PieceId = $"piece-{Random.Shared.Next(100)}",
            Position = new { X = Random.Shared.Next(1000), Y = Random.Shared.Next(1000) },
            Rotation = Random.Shared.Next(360)
        };

        await _hubConnection.InvokeAsync("MovePiece", command);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task ConcurrentConnections(int connectionCount)
    {
        var connections = new List<HubConnection>();
        var tasks = new List<Task>();

        // Create connections
        for (int i = 0; i < connectionCount; i++)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(_baseUrl)
                .Build();

            connections.Add(connection);
            tasks.Add(connection.StartAsync());
        }

        await Task.WhenAll(tasks);

        // Each connection joins a session
        tasks.Clear();
        for (int i = 0; i < connectionCount; i++)
        {
            tasks.Add(connections[i].InvokeAsync("JoinSession", $"benchmark-session-{i % 10}"));
        }

        await Task.WhenAll(tasks);

        // Cleanup
        tasks.Clear();
        foreach (var conn in connections)
        {
            tasks.Add(conn.DisposeAsync().AsTask());
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task MessageLatency()
    {
        var stopwatch = new Stopwatch();
        var tcs = new TaskCompletionSource<bool>();

        // Setup one-time handler for this benchmark run
        _hubConnection.On<string>("BenchmarkResponse", (message) =>
        {
            stopwatch.Stop();
            tcs.TrySetResult(true);
        });

        try
        {
            stopwatch.Start();
            await _hubConnection.InvokeAsync("BenchmarkEcho", "test-message");
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _hubConnection.Remove("BenchmarkResponse");
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    [Arguments(500)]
    public async Task BroadcastToGroup(int groupSize)
    {
        // Create group connections
        var connections = new List<HubConnection>();
        for (int i = 0; i < groupSize; i++)
        {
            var conn = new HubConnectionBuilder()
                .WithUrl(_baseUrl)
                .Build();
            await conn.StartAsync();
            await conn.InvokeAsync("JoinSession", "broadcast-test-session");
            connections.Add(conn);
        }

        // Broadcast message
        await _hubConnection.InvokeAsync("BroadcastToSession", "broadcast-test-session", "test-message");

        // Cleanup
        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }
    }
}