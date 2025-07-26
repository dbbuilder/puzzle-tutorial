using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private static readonly List<PuzzleSession> _sessions = new();
        private static readonly List<SessionParticipant> _participants = new();
        private static readonly List<ChatMessage> _messages = new();
        // TODO: Implement with Entity Framework
        
        public Task<PuzzleSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<PuzzleSession?>(null);
        }

        public Task<PuzzleSession> CreateSessionAsync(PuzzleSession session)
        {
            session.Id = Guid.NewGuid();
            session.JoinCode = GenerateJoinCode();
            session.CreatedAt = DateTime.UtcNow;
            session.LastActivityAt = DateTime.UtcNow;
            _sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<PuzzleSession?> GetSessionByIdAsync(Guid sessionId)
        {
            return Task.FromResult(GetSessionAsync(sessionId).Result);
        }

        public Task<PuzzleSession?> GetSessionByJoinCodeAsync(string joinCode)
        {
            var session = _sessions.FirstOrDefault(s => s.JoinCode == joinCode);
            return Task.FromResult(session);
        }

        public Task<PuzzleSession?> GetSessionWithParticipantsAsync(Guid sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.Participants = _participants.Where(p => p.SessionId == sessionId).ToList();
            }
            return Task.FromResult(session);
        }

        public Task<IEnumerable<PuzzleSession>> GetActiveSessionsForPuzzleAsync(Guid puzzleId)
        {
            var sessions = _sessions.Where(s => s.PuzzleId == puzzleId && s.Status == SessionStatus.Active);
            return Task.FromResult(sessions);
        }

        public Task<IEnumerable<PuzzleSession>> GetPublicSessionsAsync(int skip, int take)
        {
            var sessions = _sessions
                .Where(s => s.IsPublic && s.Status == SessionStatus.Active)
                .Skip(skip)
                .Take(take);
            return Task.FromResult(sessions);
        }

        public Task<bool> UpdateSessionAsync(PuzzleSession session)
        {
            var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Name = session.Name;
                existing.Status = session.Status;
                existing.IsPublic = session.IsPublic;
                existing.MaxParticipants = session.MaxParticipants;
                existing.CompletedPieces = session.CompletedPieces;
                existing.CompletionPercentage = session.CompletionPercentage;
                existing.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> UpdateSessionProgressAsync(Guid sessionId, int completedPieces, decimal completionPercentage)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.CompletedPieces = completedPieces;
                session.CompletionPercentage = completionPercentage;
                session.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteSessionAsync(Guid sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                _sessions.Remove(session);
                _participants.RemoveAll(p => p.SessionId == sessionId);
                _messages.RemoveAll(m => m.SessionId == sessionId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.Status = status;
                session.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IEnumerable<PuzzleSession>> GetActiveSessionsAsync(int limit = 10)
        {
            var sessions = _sessions
                .Where(s => s.Status == SessionStatus.Active)
                .OrderByDescending(s => s.LastActivityAt)
                .Take(limit);
            return Task.FromResult(sessions);
        }

        public Task<SessionParticipant> AddParticipantAsync(Guid sessionId, Guid userId, string? connectionId = null)
        {
            var participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                ConnectionId = connectionId,
                JoinedAt = DateTime.UtcNow,
                Status = ParticipantStatus.Active
            };
            _participants.Add(participant);
            return Task.FromResult(participant);
        }

        public Task<bool> RemoveParticipantAsync(Guid sessionId, Guid userId)
        {
            var removed = _participants.RemoveAll(p => p.SessionId == sessionId && p.UserId == userId);
            return Task.FromResult(removed > 0);
        }

        public Task<bool> UpdateParticipantStatusAsync(Guid sessionId, Guid userId, ParticipantStatus status)
        {
            var participant = _participants.FirstOrDefault(p => p.SessionId == sessionId && p.UserId == userId);
            if (participant != null)
            {
                participant.Status = status;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IEnumerable<SessionParticipant>> GetSessionParticipantsAsync(Guid sessionId)
        {
            var participants = _participants.Where(p => p.SessionId == sessionId);
            return Task.FromResult(participants);
        }

        public Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, Guid userId)
        {
            var participant = _participants.FirstOrDefault(p => p.SessionId == sessionId && p.UserId == userId);
            return Task.FromResult(participant);
        }

        public Task<ChatMessage> SaveChatMessageAsync(Guid sessionId, Guid userId, string message, MessageType messageType)
        {
            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                Message = message,
                Type = messageType,
                CreatedAt = DateTime.UtcNow
            };
            _messages.Add(chatMessage);
            return Task.FromResult(chatMessage);
        }

        public Task<bool> CompleteSessionAsync(Guid sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.Status = SessionStatus.Completed;
                session.CompletedAt = DateTime.UtcNow;
                session.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        private static string GenerateJoinCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[6];
            rng.GetBytes(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }
    }
}