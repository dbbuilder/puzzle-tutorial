using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Infrastructure.Data;
using CollaborativePuzzle.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Repositories
{
    public class SessionRepositoryTests : IDisposable
    {
        private readonly PuzzleDbContext _context;
        private readonly Mock<ILogger<SessionRepository>> _loggerMock;
        private readonly SessionRepository _repository;
        private readonly User _testUser;
        private readonly Puzzle _testPuzzle;

        public SessionRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<PuzzleDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new PuzzleDbContext(options);
            _loggerMock = new Mock<ILogger<SessionRepository>>();
            _repository = new SessionRepository(_context, _loggerMock.Object);

            // Seed test data
            _testUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTime.UtcNow
            };

            _testPuzzle = new Puzzle
            {
                Id = Guid.NewGuid(),
                Title = "Test Puzzle",
                Description = "Test Description",
                ImageUrl = "https://example.com/image.jpg",
                PieceCount = 100,
                Difficulty = PuzzleDifficulty.Medium,
                CreatedByUserId = _testUser.Id,
                CreatedAt = DateTime.UtcNow,
                Status = ParticipantStatus.Online,
                IsPublic = true
            };

            _context.Users.Add(_testUser);
            _context.Puzzles.Add(_testPuzzle);
            _context.SaveChanges();
        }

        [Fact]
        public async Task CreateSessionAsync_ShouldCreateNewSession()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Test Session",
                JoinCode = "ABC123",
                MaxParticipants = 10,
                IsPublic = true,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.CreateSessionAsync(session);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(session.Id);
            
            var savedSession = await _context.PuzzleSessions.FindAsync(session.Id);
            savedSession.Should().NotBeNull();
            savedSession!.Name.Should().Be("Test Session");
        }

        [Fact]
        public async Task GetSessionAsync_WithValidId_ShouldReturnSession()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Test Session",
                JoinCode = "XYZ789",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSessionAsync(session.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(session.Id);
            result.Name.Should().Be("Test Session");
        }

        [Fact]
        public async Task GetSessionAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetSessionAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSessionByJoinCodeAsync_WithValidCode_ShouldReturnSession()
        {
            // Arrange
            var joinCode = "JOIN123";
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Join Code Test",
                JoinCode = joinCode,
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSessionByJoinCodeAsync(joinCode);

            // Assert
            result.Should().NotBeNull();
            result!.JoinCode.Should().Be(joinCode);
            result.Status.Should().Be(SessionStatus.Active);
        }

        [Fact]
        public async Task GetSessionWithParticipantsAsync_ShouldIncludeParticipants()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Session with Participants",
                JoinCode = "PART123",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            
            var participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                UserId = _testUser.Id,
                JoinedAt = DateTime.UtcNow,
                Status = ParticipantStatus.Online
            };
            
            _context.PuzzleSessions.Add(session);
            _context.SessionParticipants.Add(participant);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSessionWithParticipantsAsync(session.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Participants.Should().HaveCount(1);
            result.Participants.First().UserId.Should().Be(_testUser.Id);
        }

        [Fact]
        public async Task GetActiveSessionsForPuzzleAsync_ShouldReturnOnlyActiveSessions()
        {
            // Arrange
            var activeSession = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Active Session",
                JoinCode = "ACTIVE1",
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            var completedSession = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Completed Session",
                JoinCode = "COMP1",
                Status = SessionStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            _context.PuzzleSessions.AddRange(activeSession, completedSession);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetActiveSessionsForPuzzleAsync(_testPuzzle.Id);

            // Assert
            result.Should().HaveCount(1);
            result.First().Status.Should().Be(SessionStatus.Active);
        }

        [Fact]
        public async Task UpdateSessionProgressAsync_ShouldUpdateProgress()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Progress Test",
                JoinCode = "PROG1",
                CompletedPieces = 0,
                CompletionPercentage = 0,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.UpdateSessionProgressAsync(session.Id, 50, 50.0m);

            // Assert
            result.Should().BeTrue();
            
            var updated = await _context.PuzzleSessions.FindAsync(session.Id);
            updated!.CompletedPieces.Should().Be(50);
            updated.CompletionPercentage.Should().Be(50.0m);
        }

        [Fact]
        public async Task AddParticipantAsync_ShouldAddNewParticipant()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Add Participant Test",
                JoinCode = "ADD1",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "newuser",
                Email = "new@example.com",
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.AddParticipantAsync(session.Id, newUser.Id, "connection123");

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be(newUser.Id);
            result.SessionId.Should().Be(session.Id);
            result.ConnectionId.Should().Be("connection123");
            
            var participants = await _context.SessionParticipants
                .Where(p => p.SessionId == session.Id)
                .ToListAsync();
            participants.Should().HaveCount(1);
        }

        [Fact]
        public async Task RemoveParticipantAsync_ShouldRemoveParticipant()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Remove Participant Test",
                JoinCode = "REM1",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            
            var participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                UserId = _testUser.Id,
                JoinedAt = DateTime.UtcNow,
                Status = ParticipantStatus.Online
            };
            
            _context.PuzzleSessions.Add(session);
            _context.SessionParticipants.Add(participant);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.RemoveParticipantAsync(session.Id, _testUser.Id);

            // Assert
            result.Should().BeTrue();
            
            var remainingParticipants = await _context.SessionParticipants
                .Where(p => p.SessionId == session.Id)
                .ToListAsync();
            remainingParticipants.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveChatMessageAsync_ShouldSaveMessage()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Chat Test",
                JoinCode = "CHAT1",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.SaveChatMessageAsync(
                session.Id, 
                _testUser.Id, 
                "Hello, world!", 
                MessageType.Chat);

            // Assert
            result.Should().NotBeNull();
            result.Message.Should().Be("Hello, world!");
            result.MessageType.Should().Be(MessageType.Chat);
            
            var messages = await _context.ChatMessages
                .Where(m => m.SessionId == session.Id)
                .ToListAsync();
            messages.Should().HaveCount(1);
        }

        [Fact]
        public async Task CompleteSessionAsync_ShouldMarkSessionAsCompleted()
        {
            // Arrange
            var session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = _testPuzzle.Id,
                CreatedByUserId = _testUser.Id,
                Name = "Complete Test",
                JoinCode = "COMP1",
                Status = SessionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _context.PuzzleSessions.Add(session);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.CompleteSessionAsync(session.Id);

            // Assert
            result.Should().BeTrue();
            
            var completed = await _context.PuzzleSessions.FindAsync(session.Id);
            completed!.Status.Should().Be(SessionStatus.Completed);
            completed.CompletedAt.Should().NotBeNull();
            completed.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}