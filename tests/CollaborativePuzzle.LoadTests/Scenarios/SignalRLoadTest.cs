using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CollaborativePuzzle.LoadTests.Scenarios;

public class SignalRLoadTest
{
    private readonly string _baseUrl;

    public SignalRLoadTest(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl;
    }

    public Scenario CreatePuzzleSessionScenario()
    {
        return Scenario.Create("puzzle_session_scenario", async context =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/puzzlehub")
                .Build();

            try
            {
                // Connect to hub
                await connection.StartAsync();

                // Join a session
                var sessionId = $"load-test-session-{context.ScenarioInfo.ThreadNumber % 10}";
                await connection.InvokeAsync("JoinSession", sessionId);

                // Simulate piece movements
                for (int i = 0; i < 5; i++)
                {
                    var moveCommand = new
                    {
                        SessionId = sessionId,
                        PieceId = $"piece-{Random.Shared.Next(100)}",
                        Position = new { X = Random.Shared.Next(1000), Y = Random.Shared.Next(1000) },
                        Rotation = Random.Shared.Next(360)
                    };

                    await connection.InvokeAsync("MovePiece", moveCommand);
                    await Task.Delay(Random.Shared.Next(100, 500));
                }

                // Send chat message
                await connection.InvokeAsync("SendMessage", sessionId, $"Message from user {context.ScenarioInfo.ThreadNumber}");

                // Leave session
                await connection.InvokeAsync("LeaveSession", sessionId);

                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail(ex.Message);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30)),
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(60)),
            Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(30))
        );
    }

    public Scenario CreateConcurrentMovementsScenario()
    {
        var connections = new List<HubConnection>();

        return Scenario.Create("concurrent_movements_scenario", async context =>
        {
            const string sessionId = "concurrent-test-session";
            var connection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/puzzlehub")
                .Build();

            try
            {
                await connection.StartAsync();
                await connection.InvokeAsync("JoinSession", sessionId);

                // Rapid piece movements
                var tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    var moveCommand = new
                    {
                        SessionId = sessionId,
                        PieceId = $"piece-{i}",
                        Position = new { X = Random.Shared.Next(1000), Y = Random.Shared.Next(1000) },
                        Rotation = Random.Shared.Next(360)
                    };

                    tasks.Add(connection.InvokeAsync("MovePiece", moveCommand));
                }

                await Task.WhenAll(tasks);
                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail(ex.Message);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        })
        .WithInit(context =>
        {
            connections.Clear();
            return Task.CompletedTask;
        })
        .WithClean(context =>
        {
            var disposeTasks = connections.Select(c => c.DisposeAsync().AsTask());
            return Task.WhenAll(disposeTasks);
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromSeconds(60))
        );
    }

    public Scenario CreateConnectionStressTest()
    {
        return Scenario.Create("connection_stress_test", async context =>
        {
            var connections = new List<HubConnection>();

            try
            {
                // Create multiple connections rapidly
                for (int i = 0; i < 10; i++)
                {
                    var connection = new HubConnectionBuilder()
                        .WithUrl($"{_baseUrl}/puzzlehub")
                        .Build();

                    await connection.StartAsync();
                    connections.Add(connection);
                }

                // Keep connections alive for a bit
                await Task.Delay(Random.Shared.Next(1000, 3000));

                // Dispose all connections
                foreach (var conn in connections)
                {
                    await conn.DisposeAsync();
                }

                return Response.Ok();
            }
            catch (Exception ex)
            {
                // Clean up any successful connections
                foreach (var conn in connections)
                {
                    try { await conn.DisposeAsync(); } catch { }
                }
                return Response.Fail(ex.Message);
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(30)),
            Simulation.KeepConstant(copies: 20, during: TimeSpan.FromSeconds(60))
        );
    }
}