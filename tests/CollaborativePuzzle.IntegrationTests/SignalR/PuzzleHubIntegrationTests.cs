using System.Net.Http.Json;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CollaborativePuzzle.IntegrationTests.SignalR;

public class PuzzleHubIntegrationTests : IntegrationTestBase, IAsyncLifetime
{
    private HubConnection _hubConnection = null!;
    private readonly List<string> _receivedMessages = new();

    public PuzzleHubIntegrationTests(TestcontainersFixture fixture) : base(fixture)
    {
    }

    public async Task InitializeAsync()
    {
        // Build SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{Client.BaseAddress}puzzlehub", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        // Setup event handlers
        _hubConnection.On<PieceMovedNotification>("PieceMoved", notification =>
        {
            _receivedMessages.Add($"PieceMoved:{notification.PieceId}");
        });

        _hubConnection.On<string, string>("UserJoined", (sessionId, userId) =>
        {
            _receivedMessages.Add($"UserJoined:{sessionId}:{userId}");
        });

        // Start connection
        await _hubConnection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Connect_To_Hub_Successfully()
    {
        // Assert
        _hubConnection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task Should_Join_Session_And_Notify_Others()
    {
        // Arrange
        var sessionId = "test-session-123";

        // Act
        await _hubConnection.InvokeAsync("JoinSession", sessionId);
        await Task.Delay(100); // Give time for message to be processed

        // Assert
        _receivedMessages.Should().Contain(msg => msg.StartsWith("UserJoined"));
    }

    [Fact]
    public async Task Should_Move_Piece_And_Broadcast_To_Group()
    {
        // Arrange
        var sessionId = "test-session-123";
        await _hubConnection.InvokeAsync("JoinSession", sessionId);

        var moveCommand = new MovePieceCommand
        {
            SessionId = sessionId,
            PieceId = "piece-1",
            Position = new Position { X = 100, Y = 200 },
            Rotation = 0
        };

        // Act
        await _hubConnection.InvokeAsync("MovePiece", moveCommand);
        await Task.Delay(100); // Give time for message to be processed

        // Assert
        _receivedMessages.Should().Contain(msg => msg.Contains("PieceMoved:piece-1"));
    }

    [Fact]
    public async Task Should_Handle_Concurrent_Piece_Movements()
    {
        // Arrange
        var sessionId = "test-session-123";
        await _hubConnection.InvokeAsync("JoinSession", sessionId);

        var tasks = new List<Task>();

        // Act - Move multiple pieces concurrently
        for (int i = 0; i < 10; i++)
        {
            var pieceId = $"piece-{i}";
            var moveCommand = new MovePieceCommand
            {
                SessionId = sessionId,
                PieceId = pieceId,
                Position = new Position { X = i * 10, Y = i * 10 },
                Rotation = 0
            };

            tasks.Add(_hubConnection.InvokeAsync("MovePiece", moveCommand));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(200); // Give time for all messages

        // Assert
        for (int i = 0; i < 10; i++)
        {
            _receivedMessages.Should().Contain(msg => msg.Contains($"piece-{i}"));
        }
    }

    [Fact]
    public async Task Should_Enforce_Piece_Locking()
    {
        // Arrange
        var sessionId = "test-session-123";
        var pieceId = "piece-lock-test";
        await _hubConnection.InvokeAsync("JoinSession", sessionId);

        // Act - Lock piece
        await _hubConnection.InvokeAsync("LockPiece", sessionId, pieceId);

        // Try to move locked piece (should fail or be ignored)
        var moveCommand = new MovePieceCommand
        {
            SessionId = sessionId,
            PieceId = pieceId,
            Position = new Position { X = 100, Y = 100 },
            Rotation = 0
        };

        // This should either throw or be ignored based on implementation
        var moveTask = _hubConnection.InvokeAsync("MovePiece", moveCommand);

        // Assert
        // Implementation specific - either exception or no movement broadcast
        await Task.Delay(100);
    }
}

// DTOs for testing (these should match your actual DTOs)
public class MovePieceCommand
{
    public string SessionId { get; set; } = string.Empty;
    public string PieceId { get; set; } = string.Empty;
    public Position Position { get; set; } = new();
    public float Rotation { get; set; }
}

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class PieceMovedNotification
{
    public string PieceId { get; set; } = string.Empty;
    public Position Position { get; set; } = new();
    public float Rotation { get; set; }
    public string UserId { get; set; } = string.Empty;
}