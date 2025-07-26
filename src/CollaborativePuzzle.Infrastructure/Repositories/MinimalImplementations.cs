using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Core.Enums;
using Microsoft.Extensions.Logging;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    // Minimal implementations for build testing
    
    public class MinimalSessionRepository : ISessionRepository
    {
        private readonly ILogger<MinimalSessionRepository> _logger;

        public MinimalSessionRepository(ILogger<MinimalSessionRepository> logger)
        {
            _logger = logger;
        }

        public Task<PuzzleSession> CreateSessionAsync(PuzzleSession session) => Task.FromResult(session);
        public Task<PuzzleSession?> GetSessionAsync(Guid sessionId) => Task.FromResult<PuzzleSession?>(null);
        public Task<PuzzleSession?> GetSessionByIdAsync(Guid sessionId) => Task.FromResult<PuzzleSession?>(null);
        public Task<PuzzleSession?> GetSessionByJoinCodeAsync(string joinCode) => Task.FromResult<PuzzleSession?>(null);
        public Task<PuzzleSession?> GetSessionWithParticipantsAsync(Guid sessionId) => Task.FromResult<PuzzleSession?>(null);
        public Task<IEnumerable<PuzzleSession>> GetActiveSessionsForPuzzleAsync(Guid puzzleId) => Task.FromResult<IEnumerable<PuzzleSession>>(Array.Empty<PuzzleSession>());
        public Task<IEnumerable<PuzzleSession>> GetPublicSessionsAsync(int skip, int take) => Task.FromResult<IEnumerable<PuzzleSession>>(Array.Empty<PuzzleSession>());
        public Task<bool> UpdateSessionAsync(PuzzleSession session) => Task.FromResult(false);
        public Task<bool> UpdateSessionProgressAsync(Guid sessionId, int completedPieces, decimal completionPercentage) => Task.FromResult(false);
        public Task<bool> DeleteSessionAsync(Guid sessionId) => Task.FromResult(false);
        public Task<SessionParticipant> AddParticipantAsync(Guid sessionId, Guid userId, string? connectionId = null) => Task.FromResult(new SessionParticipant());
        public Task<bool> RemoveParticipantAsync(Guid sessionId, Guid userId) => Task.FromResult(false);
        public Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, Guid userId) => Task.FromResult<SessionParticipant?>(null);
        public Task<IEnumerable<SessionParticipant>> GetSessionParticipantsAsync(Guid sessionId) => Task.FromResult<IEnumerable<SessionParticipant>>(Array.Empty<SessionParticipant>());
        public Task<ChatMessage> SaveChatMessageAsync(Guid sessionId, Guid userId, string message, MessageType messageType) => Task.FromResult(new ChatMessage());
        public Task<bool> CompleteSessionAsync(Guid sessionId) => Task.FromResult(false);
        
        // Legacy methods
        public Task<PuzzleSession> CreateSessionAsync(Guid puzzleId, Guid createdByUserId) => Task.FromResult(new PuzzleSession());
        public Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status) => Task.FromResult(false);
        public Task<IEnumerable<PuzzleSession>> GetActiveSessionsAsync(int limit = 10) => Task.FromResult<IEnumerable<PuzzleSession>>(Array.Empty<PuzzleSession>());
        public Task<bool> AddParticipantAsync(Guid sessionId, Guid userId, string displayName) => Task.FromResult(false);
        public Task<bool> UpdateParticipantStatusAsync(Guid sessionId, Guid userId, ParticipantStatus status) => Task.FromResult(false);
    }

    public class MinimalPieceRepository : IPieceRepository
    {
        public Task<PuzzlePiece?> GetPieceAsync(Guid pieceId) => Task.FromResult<PuzzlePiece?>(null);
        public Task<IEnumerable<PuzzlePiece>> GetPuzzlePiecesAsync(Guid puzzleId) => Task.FromResult<IEnumerable<PuzzlePiece>>(Array.Empty<PuzzlePiece>());
        public Task<bool> UpdatePiecePositionAsync(Guid pieceId, double x, double y, int rotation) => Task.FromResult(false);
        public Task<bool> LockPieceAsync(Guid pieceId, Guid userId) => Task.FromResult(false);
        public Task<bool> UnlockPieceAsync(Guid pieceId) => Task.FromResult(false);
        public Task<bool> UnlockAllPiecesForUserAsync(Guid userId) => Task.FromResult(false);
        public Task<IEnumerable<PuzzlePiece>> GetLockedPiecesAsync(Guid puzzleId) => Task.FromResult<IEnumerable<PuzzlePiece>>(Array.Empty<PuzzlePiece>());
        public Task<bool> SetPieceAsPlacedAsync(Guid pieceId, Guid placedByUserId) => Task.FromResult(false);
    }

    public class MinimalPuzzleRepository : IPuzzleRepository
    {
        public Task<Puzzle?> GetPuzzleAsync(Guid puzzleId) => Task.FromResult<Puzzle?>(null);
        public Task<IEnumerable<Puzzle>> GetPuzzlesAsync(int page = 1, int pageSize = 20) => Task.FromResult<IEnumerable<Puzzle>>(Array.Empty<Puzzle>());
        public Task<Puzzle> CreatePuzzleAsync(string title, string imageUrl, int pieceCount, int width, int height, Guid createdByUserId) => Task.FromResult(new Puzzle());
        public Task<bool> UpdatePuzzleAsync(Puzzle puzzle) => Task.FromResult(false);
        public Task<bool> DeletePuzzleAsync(Guid puzzleId) => Task.FromResult(false);
        public Task<IEnumerable<Puzzle>> GetPuzzlesByDifficultyAsync(PuzzleDifficulty difficulty, int limit = 10) => Task.FromResult<IEnumerable<Puzzle>>(Array.Empty<Puzzle>());
    }

    public class MinimalUserRepository : IUserRepository
    {
        public Task<User?> GetUserAsync(Guid userId) => Task.FromResult<User?>(null);
        public Task<User?> GetUserByUsernameAsync(string username) => Task.FromResult<User?>(null);
        public Task<User?> GetUserByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<User> CreateUserAsync(string username, string email, string passwordHash) => Task.FromResult(new User());
        public Task<bool> UpdateUserAsync(User user) => Task.FromResult(false);
        public Task<bool> UpdateLastActiveAsync(Guid userId) => Task.FromResult(false);
        public Task<IEnumerable<User>> GetActiveUsersAsync(int minutes = 30, int limit = 50) => Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
    }
}