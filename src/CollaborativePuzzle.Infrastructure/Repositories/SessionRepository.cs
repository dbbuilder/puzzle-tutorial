using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        // TODO: Implement with Entity Framework
        
        public Task<PuzzleSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<PuzzleSession?>(null);
        }

        public Task<PuzzleSession> CreateSessionAsync(Guid puzzleId, Guid createdByUserId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status)
        {
            return Task.FromResult(false);
        }

        public Task<IEnumerable<PuzzleSession>> GetActiveSessionsAsync(int limit = 10)
        {
            return Task.FromResult<IEnumerable<PuzzleSession>>(Array.Empty<PuzzleSession>());
        }

        public Task<bool> AddParticipantAsync(Guid sessionId, Guid userId, string displayName)
        {
            return Task.FromResult(false);
        }

        public Task<bool> RemoveParticipantAsync(Guid sessionId, Guid userId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> UpdateParticipantStatusAsync(Guid sessionId, Guid userId, ParticipantStatus status)
        {
            return Task.FromResult(false);
        }

        public Task<IEnumerable<SessionParticipant>> GetSessionParticipantsAsync(Guid sessionId)
        {
            return Task.FromResult<IEnumerable<SessionParticipant>>(Array.Empty<SessionParticipant>());
        }
    }
}