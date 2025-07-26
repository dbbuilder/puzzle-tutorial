using System;
using System.Collections.Generic;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Tests.Helpers
{
    /// <summary>
    /// Fluent builder for creating test data with sensible defaults.
    /// Follows the Builder pattern to make test data creation more readable and maintainable.
    /// </summary>
    public static class TestDataBuilder
    {
        /// <summary>
        /// Creates a new user builder.
        /// </summary>
        public static UserBuilder User() => new UserBuilder();

        /// <summary>
        /// Creates a new puzzle builder.
        /// </summary>
        public static PuzzleBuilder Puzzle() => new PuzzleBuilder();

        /// <summary>
        /// Creates a new puzzle piece builder.
        /// </summary>
        public static PuzzlePieceBuilder PuzzlePiece() => new PuzzlePieceBuilder();

        /// <summary>
        /// Creates a new session builder.
        /// </summary>
        public static SessionBuilder Session() => new SessionBuilder();

        /// <summary>
        /// Creates a new participant builder.
        /// </summary>
        public static ParticipantBuilder Participant() => new ParticipantBuilder();

        /// <summary>
        /// Creates a new chat message builder.
        /// </summary>
        public static ChatMessageBuilder ChatMessage() => new ChatMessageBuilder();
    }

    /// <summary>
    /// Builder for creating test users.
    /// </summary>
    public class UserBuilder
    {
        private readonly User _user;

        public UserBuilder()
        {
            _user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"testuser_{Guid.NewGuid():N}",
                Email = $"test_{Guid.NewGuid():N}@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsActive = true,
                PreferredLanguage = "en"
            };
        }

        public UserBuilder WithId(Guid id)
        {
            _user.Id = id;
            return this;
        }

        public UserBuilder WithUsername(string username)
        {
            _user.Username = username;
            return this;
        }

        public UserBuilder WithEmail(string email)
        {
            _user.Email = email;
            return this;
        }

        public UserBuilder WithDisplayName(string displayName)
        {
            _user.DisplayName = displayName;
            return this;
        }

        public UserBuilder WithExternalAuth(string provider, string externalId)
        {
            _user.Provider = provider;
            _user.ExternalId = externalId;
            return this;
        }

        public UserBuilder AsInactive()
        {
            _user.IsActive = false;
            return this;
        }

        public User Build() => _user;
    }

    /// <summary>
    /// Builder for creating test puzzles.
    /// </summary>
    public class PuzzleBuilder
    {
        private readonly Puzzle _puzzle;
        private readonly List<PuzzlePiece> _pieces = new();

        public PuzzleBuilder()
        {
            _puzzle = new Puzzle
            {
                Id = Guid.NewGuid(),
                Title = $"Test Puzzle {Guid.NewGuid():N}",
                Description = "A test puzzle for unit testing",
                ImageUrl = "https://example.com/test-puzzle.jpg",
                PiecesDataUrl = "https://example.com/test-puzzle-pieces.json",
                ImageFileName = "test-puzzle.jpg",
                ImageContentType = "image/jpeg",
                PieceCount = 100,
                Width = 1000,
                Height = 800,
                Difficulty = PuzzleDifficulty.Medium,
                Category = "Test",
                Tags = "test,sample",
                IsPublic = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = Guid.NewGuid()
            };
        }

        public PuzzleBuilder WithId(Guid id)
        {
            _puzzle.Id = id;
            return this;
        }

        public PuzzleBuilder WithTitle(string title)
        {
            _puzzle.Title = title;
            return this;
        }

        public PuzzleBuilder WithPieceCount(int count)
        {
            _puzzle.PieceCount = count;
            return this;
        }

        public PuzzleBuilder WithDifficulty(PuzzleDifficulty difficulty)
        {
            _puzzle.Difficulty = difficulty;
            return this;
        }

        public PuzzleBuilder WithCreator(Guid userId)
        {
            _puzzle.CreatedByUserId = userId;
            return this;
        }

        public PuzzleBuilder AsPrivate()
        {
            _puzzle.IsPublic = false;
            return this;
        }

        public PuzzleBuilder WithPieces(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pieces.Add(new PuzzlePiece
                {
                    Id = Guid.NewGuid(),
                    PuzzleId = _puzzle.Id,
                    PieceNumber = i,
                    CurrentX = i % 10 * 100,
                    CurrentY = i / 10 * 100,
                    CorrectX = i % 10 * 100,
                    CorrectY = i / 10 * 100,
                    Rotation = 0,
                    ShapeData = $"{{\"type\":\"standard\",\"edges\":[1,0,1,0]}}",
                    IsPlaced = false
                });
            }
            return this;
        }

        public Puzzle Build()
        {
            _puzzle.Pieces = _pieces;
            return _puzzle;
        }
    }

    /// <summary>
    /// Builder for creating test puzzle pieces.
    /// </summary>
    public class PuzzlePieceBuilder
    {
        private readonly PuzzlePiece _piece;

        public PuzzlePieceBuilder()
        {
            _piece = new PuzzlePiece
            {
                Id = Guid.NewGuid(),
                PuzzleId = Guid.NewGuid(),
                PieceNumber = 0,
                CurrentX = 100,
                CurrentY = 100,
                CorrectX = 200,
                CorrectY = 200,
                Rotation = 0,
                ShapeData = "{\"type\":\"standard\",\"edges\":[1,0,1,0]}",
                IsPlaced = false
            };
        }

        public PuzzlePieceBuilder WithId(Guid id)
        {
            _piece.Id = id;
            return this;
        }

        public PuzzlePieceBuilder ForPuzzle(Guid puzzleId)
        {
            _piece.PuzzleId = puzzleId;
            return this;
        }

        public PuzzlePieceBuilder AtPosition(double x, double y)
        {
            _piece.CurrentX = x;
            _piece.CurrentY = y;
            return this;
        }

        public PuzzlePieceBuilder WithCorrectPosition(double x, double y)
        {
            _piece.CorrectX = x;
            _piece.CorrectY = y;
            return this;
        }

        public PuzzlePieceBuilder AsPlaced()
        {
            _piece.IsPlaced = true;
            _piece.CurrentX = _piece.CorrectX;
            _piece.CurrentY = _piece.CorrectY;
            return this;
        }

        public PuzzlePieceBuilder LockedBy(Guid userId)
        {
            _piece.LockedByUserId = userId;
            _piece.LockedAt = DateTime.UtcNow;
            return this;
        }

        public PuzzlePiece Build() => _piece;
    }

    /// <summary>
    /// Builder for creating test sessions.
    /// </summary>
    public class SessionBuilder
    {
        private readonly PuzzleSession _session;
        private readonly List<SessionParticipant> _participants = new();

        public SessionBuilder()
        {
            _session = new PuzzleSession
            {
                Id = Guid.NewGuid(),
                PuzzleId = Guid.NewGuid(),
                Name = $"Test Session {Guid.NewGuid():N}",
                Description = "A test session",
                JoinCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                Status = SessionStatus.InProgress,
                IsPublic = true,
                MaxParticipants = 10,
                CompletionPercentage = 0,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                CreatedByUserId = Guid.NewGuid()
            };
        }

        public SessionBuilder WithId(Guid id)
        {
            _session.Id = id;
            return this;
        }

        public SessionBuilder WithName(string name)
        {
            _session.Name = name;
            return this;
        }

        public SessionBuilder ForPuzzle(Guid puzzleId)
        {
            _session.PuzzleId = puzzleId;
            return this;
        }

        public SessionBuilder WithJoinCode(string code)
        {
            _session.JoinCode = code;
            return this;
        }

        public SessionBuilder WithStatus(SessionStatus status)
        {
            _session.Status = status;
            return this;
        }

        public SessionBuilder AsPrivate(string? password = null)
        {
            _session.IsPublic = false;
            _session.Password = password;
            return this;
        }

        public SessionBuilder WithParticipant(Guid userId, ParticipantRole role = ParticipantRole.Player)
        {
            _participants.Add(new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionId = _session.Id,
                UserId = userId,
                Role = role,
                Status = ParticipantStatus.Active,
                JoinedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            });
            return this;
        }

        public PuzzleSession Build()
        {
            _session.Participants = _participants;
            return _session;
        }
    }

    /// <summary>
    /// Builder for creating test participants.
    /// </summary>
    public class ParticipantBuilder
    {
        private readonly SessionParticipant _participant;

        public ParticipantBuilder()
        {
            _participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Role = ParticipantRole.Player,
                Status = ParticipantStatus.Active,
                JoinedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                PiecesPlaced = 0,
                TotalActiveTime = TimeSpan.Zero
            };
        }

        public ParticipantBuilder WithId(Guid id)
        {
            _participant.Id = id;
            return this;
        }

        public ParticipantBuilder InSession(Guid sessionId)
        {
            _participant.SessionId = sessionId;
            return this;
        }

        public ParticipantBuilder AsUser(Guid userId)
        {
            _participant.UserId = userId;
            return this;
        }

        public ParticipantBuilder WithRole(ParticipantRole role)
        {
            _participant.Role = role;
            return this;
        }

        public ParticipantBuilder WithStatus(ParticipantStatus status)
        {
            _participant.Status = status;
            return this;
        }

        public ParticipantBuilder WithConnectionId(string connectionId)
        {
            _participant.ConnectionId = connectionId;
            return this;
        }

        public SessionParticipant Build() => _participant;
    }

    /// <summary>
    /// Builder for creating test chat messages.
    /// </summary>
    public class ChatMessageBuilder
    {
        private readonly ChatMessage _message;

        public ChatMessageBuilder()
        {
            _message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Message = "Test message",
                Type = MessageType.Chat,
                CreatedAt = DateTime.UtcNow
            };
        }

        public ChatMessageBuilder WithId(Guid id)
        {
            _message.Id = id;
            return this;
        }

        public ChatMessageBuilder InSession(Guid sessionId)
        {
            _message.SessionId = sessionId;
            return this;
        }

        public ChatMessageBuilder FromUser(Guid userId)
        {
            _message.UserId = userId;
            return this;
        }

        public ChatMessageBuilder WithMessage(string message)
        {
            _message.Message = message;
            return this;
        }

        public ChatMessageBuilder WithType(MessageType type)
        {
            _message.Type = type;
            return this;
        }

        public ChatMessageBuilder AsReplyTo(Guid messageId)
        {
            _message.ReplyToMessageId = messageId;
            return this;
        }

        public ChatMessage Build() => _message;
    }
}