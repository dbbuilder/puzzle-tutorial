using System;
using System.Threading;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Hubs;
using CollaborativePuzzle.Tests.Helpers;
using CollaborativePuzzle.Tests.TestBase;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CollaborativePuzzle.Tests.Hubs
{
    /// <summary>
    /// Unit tests for PuzzleHub demonstrating TDD for SignalR with Redis backplane.
    /// These tests drive the implementation of real-time puzzle collaboration features.
    /// </summary>
    public class PuzzleHubTests : TestBase
    {
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<IClientProxy> _callerMock;
        private readonly Mock<IClientProxy> _othersMock;
        private readonly Mock<IClientProxy> _groupMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly Mock<HubCallerContext> _contextMock;
        
        private readonly Mock<ISessionRepository> _sessionRepositoryMock;
        private readonly Mock<IPieceRepository> _pieceRepositoryMock;
        private readonly Mock<IRedisService> _redisMock;
        
        private readonly PuzzleHub _hub;

        public PuzzleHubTests(ITestOutputHelper output) : base(output)
        {
            // Setup SignalR mocks
            _clientsMock = CreateMock<IHubCallerClients>();
            _callerMock = CreateMock<IClientProxy>();
            _othersMock = CreateMock<IClientProxy>();
            _groupMock = CreateMock<IClientProxy>();
            _groupsMock = CreateMock<IGroupManager>();
            _contextMock = CreateMock<HubCallerContext>();
            
            // Setup repository mocks
            _sessionRepositoryMock = CreateMock<ISessionRepository>();
            _pieceRepositoryMock = CreateMock<IPieceRepository>();
            _redisMock = CreateMock<IRedisService>();
            
            // Configure SignalR mock behaviors
            _clientsMock.Setup(x => x.Caller).Returns(_callerMock.Object);
            _clientsMock.Setup(x => x.Others).Returns(_othersMock.Object);
            _clientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_groupMock.Object);
            _clientsMock.Setup(x => x.OthersInGroup(It.IsAny<string>())).Returns(_othersMock.Object);
            
            _contextMock.Setup(x => x.ConnectionId).Returns("test-connection-id");
            _contextMock.Setup(x => x.UserIdentifier).Returns("test-user-id");
            
            // Create the hub
            _hub = new PuzzleHub(
                _sessionRepositoryMock.Object,
                _pieceRepositoryMock.Object,
                _redisMock.Object,
                GetService<ILogger<PuzzleHub>>()
            )
            {
                Clients = _clientsMock.Object,
                Groups = _groupsMock.Object,
                Context = _contextMock.Object
            };
        }

        [Fact]
        public async Task JoinPuzzleSession_WithValidSession_ShouldAddUserToGroupAndNotifyOthers()
        {
            LogTestStep("Testing user joining a valid puzzle session");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var connectionId = "test-connection-123";
            
            var session = TestDataBuilder.Session()
                .WithId(sessionId)
                .WithStatus(SessionStatus.InProgress)
                .Build();
            
            var participant = TestDataBuilder.Participant()
                .InSession(sessionId)
                .AsUser(userId)
                .WithConnectionId(connectionId)
                .Build();
            
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _sessionRepositoryMock
                .Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync(session);
            
            _sessionRepositoryMock
                .Setup(x => x.AddParticipantAsync(sessionId, userId, connectionId))
                .ReturnsAsync(participant);
            
            _groupsMock
                .Setup(x => x.AddToGroupAsync(connectionId, $"puzzle-{sessionId}", default))
                .Returns(Task.CompletedTask);
            
            _redisMock
                .Setup(x => x.SetAsync($"connection:{connectionId}", It.IsAny<object>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);
            
            // Act
            var result = await _hub.JoinPuzzleSession(sessionId.ToString());
            
            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.SessionId.Should().Be(sessionId.ToString());
            
            // Verify user was added to SignalR group
            _groupsMock.Verify(x => x.AddToGroupAsync(
                connectionId, 
                $"puzzle-{sessionId}", 
                default
            ), Times.Once);
            
            // Verify others were notified
            _othersMock.Verify(x => x.SendAsync(
                "UserJoined",
                It.Is<object[]>(args => 
                    args.Length >= 1 && 
                    args[0] is UserJoinedNotification notification &&
                    notification.UserId == userId.ToString()
                ),
                default
            ), Times.Once);
            
            // Verify connection tracking in Redis
            _redisMock.Verify(x => x.SetAsync(
                $"connection:{connectionId}",
                It.IsAny<object>(),
                It.IsAny<TimeSpan>()
            ), Times.Once);
        }

        [Fact]
        public async Task MovePiece_WithValidMove_ShouldUpdatePositionAndNotifyOthers()
        {
            LogTestStep("Testing piece movement with valid data");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var pieceId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var newX = 150.5;
            var newY = 200.75;
            var rotation = 90;
            
            var piece = TestDataBuilder.PuzzlePiece()
                .WithId(pieceId)
                .AtPosition(100, 100)
                .Build();
            
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            // User should be in a session (tracked in Redis)
            _redisMock
                .Setup(x => x.GetAsync<string>($"user:{userId}:session"))
                .ReturnsAsync(sessionId.ToString());
            
            _pieceRepositoryMock
                .Setup(x => x.GetPieceAsync(pieceId))
                .ReturnsAsync(piece);
            
            _pieceRepositoryMock
                .Setup(x => x.UpdatePiecePositionAsync(pieceId, newX, newY, rotation))
                .ReturnsAsync(true);
            
            // Act
            var result = await _hub.MovePiece(pieceId.ToString(), newX, newY, rotation);
            
            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.PieceId.Should().Be(pieceId.ToString());
            result.NewPosition.Should().NotBeNull();
            result.NewPosition!.X.Should().Be(newX);
            result.NewPosition.Y.Should().Be(newY);
            result.NewPosition.Rotation.Should().Be(rotation);
            
            // Verify others in group were notified
            _othersMock.Verify(x => x.SendAsync(
                "PieceMoved",
                It.Is<object[]>(args => 
                    args.Length >= 1 && 
                    args[0] is PieceMovedNotification notification &&
                    notification.PieceId == pieceId.ToString() &&
                    notification.X == newX &&
                    notification.Y == newY &&
                    notification.MovedByUserId == userId.ToString()
                ),
                default
            ), Times.Once);
        }

        [Fact]
        public async Task LockPiece_WhenPieceIsAvailable_ShouldLockAndNotifyOthers()
        {
            LogTestStep("Testing piece locking mechanism");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var pieceId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            var piece = TestDataBuilder.PuzzlePiece()
                .WithId(pieceId)
                .Build(); // Not locked
            
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _redisMock
                .Setup(x => x.GetAsync<string>($"user:{userId}:session"))
                .ReturnsAsync(sessionId.ToString());
            
            _pieceRepositoryMock
                .Setup(x => x.GetPieceAsync(pieceId))
                .ReturnsAsync(piece);
            
            _pieceRepositoryMock
                .Setup(x => x.LockPieceAsync(pieceId, userId))
                .ReturnsAsync(true);
            
            // Distributed lock via Redis
            _redisMock
                .Setup(x => x.SetAsync(
                    $"piece-lock:{pieceId}", 
                    userId.ToString(), 
                    It.IsAny<TimeSpan>(), 
                    When.NotExists))
                .ReturnsAsync(true);
            
            // Act
            var result = await _hub.LockPiece(pieceId.ToString());
            
            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.PieceId.Should().Be(pieceId.ToString());
            result.LockedBy.Should().Be(userId.ToString());
            
            // Verify distributed lock was acquired
            _redisMock.Verify(x => x.SetAsync(
                $"piece-lock:{pieceId}",
                userId.ToString(),
                It.Is<TimeSpan>(t => t.TotalSeconds >= 30),
                When.NotExists
            ), Times.Once);
            
            // Verify others were notified
            _othersMock.Verify(x => x.SendAsync(
                "PieceLocked",
                It.Is<object[]>(args => 
                    args[0] is PieceLockedNotification notification &&
                    notification.PieceId == pieceId.ToString() &&
                    notification.LockedByUserId == userId.ToString()
                ),
                default
            ), Times.Once);
        }

        [Fact]
        public async Task LockPiece_WhenPieceIsAlreadyLocked_ShouldReturnFailure()
        {
            LogTestStep("Testing piece locking when already locked by another user");
            
            // Arrange
            var pieceId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            
            var piece = TestDataBuilder.PuzzlePiece()
                .WithId(pieceId)
                .LockedBy(otherUserId)
                .Build();
            
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _pieceRepositoryMock
                .Setup(x => x.GetPieceAsync(pieceId))
                .ReturnsAsync(piece);
            
            // Distributed lock should fail
            _redisMock
                .Setup(x => x.SetAsync(
                    $"piece-lock:{pieceId}", 
                    userId.ToString(), 
                    It.IsAny<TimeSpan>(), 
                    When.NotExists))
                .ReturnsAsync(false);
            
            // Act
            var result = await _hub.LockPiece(pieceId.ToString());
            
            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Error.Should().Contain("already locked");
            result.LockedBy.Should().Be(otherUserId.ToString());
            
            // Should not update database
            _pieceRepositoryMock.Verify(x => x.LockPieceAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            
            // Should not notify others
            _othersMock.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
        }

        [Fact]
        public async Task SendChatMessage_WithValidMessage_ShouldBroadcastToGroup()
        {
            LogTestStep("Testing chat message broadcasting");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var message = "Hello, puzzle friends!";
            
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _redisMock
                .Setup(x => x.GetAsync<string>($"user:{userId}:session"))
                .ReturnsAsync(sessionId.ToString());
            
            _sessionRepositoryMock
                .Setup(x => x.SaveChatMessageAsync(sessionId, userId, message, MessageType.Chat))
                .ReturnsAsync(TestDataBuilder.ChatMessage()
                    .InSession(sessionId)
                    .FromUser(userId)
                    .WithMessage(message)
                    .Build());
            
            // Act
            await _hub.SendChatMessage(message);
            
            // Assert
            _groupMock.Verify(x => x.SendAsync(
                "ChatMessage",
                It.Is<object[]>(args =>
                    args[0] is ChatMessageNotification notification &&
                    notification.Message == message &&
                    notification.UserId == userId.ToString() &&
                    notification.SessionId == sessionId.ToString()
                ),
                default
            ), Times.Once);
        }

        [Fact]
        public async Task UpdateCursor_ShouldThrottleAndBatchUpdates()
        {
            LogTestStep("Testing cursor position updates with throttling");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _redisMock
                .Setup(x => x.GetAsync<string>($"user:{userId}:session"))
                .ReturnsAsync(sessionId.ToString());
            
            // Redis pub/sub for cursor updates
            _redisMock
                .Setup(x => x.PublishAsync($"cursor:{sessionId}", It.IsAny<object>()))
                .Returns(Task.CompletedTask);
            
            // Act - Send multiple cursor updates rapidly
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                var x = i * 10;
                var y = i * 10;
                tasks[i] = _hub.UpdateCursor(x, y);
            }
            
            await Task.WhenAll(tasks);
            
            // Assert - Should be throttled, not all 10 publishes
            _redisMock.Verify(x => x.PublishAsync(
                $"cursor:{sessionId}",
                It.IsAny<object>()
            ), Times.AtMost(3)); // Throttled to reduce load
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldCleanupUserStateAndNotifyOthers()
        {
            LogTestStep("Testing cleanup on user disconnection");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var connectionId = "test-connection-123";
            
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);
            _contextMock.Setup(x => x.UserIdentifier).Returns(userId.ToString());
            
            _redisMock
                .Setup(x => x.GetAsync<string>($"connection:{connectionId}:session"))
                .ReturnsAsync(sessionId.ToString());
            
            _sessionRepositoryMock
                .Setup(x => x.RemoveParticipantAsync(sessionId, userId))
                .Returns(Task.CompletedTask);
            
            _pieceRepositoryMock
                .Setup(x => x.UnlockAllPiecesForUserAsync(userId))
                .Returns(Task.CompletedTask);
            
            _redisMock
                .Setup(x => x.DeleteAsync($"connection:{connectionId}"))
                .Returns(Task.CompletedTask);
            
            _redisMock
                .Setup(x => x.DeleteAsync($"user:{userId}:session"))
                .Returns(Task.CompletedTask);
            
            // Act
            await _hub.OnDisconnectedAsync(null);
            
            // Assert
            // Verify cleanup occurred
            _sessionRepositoryMock.Verify(x => x.RemoveParticipantAsync(sessionId, userId), Times.Once);
            _pieceRepositoryMock.Verify(x => x.UnlockAllPiecesForUserAsync(userId), Times.Once);
            _redisMock.Verify(x => x.DeleteAsync($"connection:{connectionId}"), Times.Once);
            
            // Verify others were notified
            _othersMock.Verify(x => x.SendAsync(
                "UserLeft",
                It.Is<object[]>(args =>
                    args[0] is UserLeftNotification notification &&
                    notification.UserId == userId.ToString() &&
                    notification.SessionId == sessionId.ToString()
                ),
                default
            ), Times.Once);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task CheckPuzzleCompletion_WhenAllPiecesPlaced_ShouldNotifyCompletion(int pieceCount)
        {
            LogTestStep($"Testing puzzle completion detection with {pieceCount} pieces");
            
            // Arrange
            var sessionId = Guid.NewGuid();
            var puzzleId = Guid.NewGuid();
            
            var session = TestDataBuilder.Session()
                .WithId(sessionId)
                .ForPuzzle(puzzleId)
                .Build();
            
            _sessionRepositoryMock
                .Setup(x => x.GetSessionAsync(sessionId))
                .ReturnsAsync(session);
            
            _pieceRepositoryMock
                .Setup(x => x.GetPlacedPieceCountAsync(puzzleId))
                .ReturnsAsync(pieceCount);
            
            _pieceRepositoryMock
                .Setup(x => x.GetTotalPieceCountAsync(puzzleId))
                .ReturnsAsync(pieceCount);
            
            _sessionRepositoryMock
                .Setup(x => x.CompleteSessionAsync(sessionId))
                .Returns(Task.CompletedTask);
            
            // Act
            await _hub.CheckPuzzleCompletion(sessionId.ToString());
            
            // Assert
            _groupMock.Verify(x => x.SendAsync(
                "PuzzleCompleted",
                It.Is<object[]>(args =>
                    args[0] is PuzzleCompletedNotification notification &&
                    notification.SessionId == sessionId.ToString()
                ),
                default
            ), Times.Once);
            
            _sessionRepositoryMock.Verify(x => x.CompleteSessionAsync(sessionId), Times.Once);
        }
    }
}