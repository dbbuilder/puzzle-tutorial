using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    /// <summary>
    /// Entity Framework implementation of ISessionRepository
    /// </summary>
    public class SessionRepository : ISessionRepository
    {
        private readonly PuzzleDbContext _context;
        private readonly ILogger<SessionRepository> _logger;

        public SessionRepository(PuzzleDbContext context, ILogger<SessionRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PuzzleSession?> GetSessionAsync(Guid sessionId)
        {
            return await _context.PuzzleSessions
                .Include(s => s.Puzzle)
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<PuzzleSession> CreateSessionAsync(PuzzleSession session)
        {
            try
            {
                if (string.IsNullOrEmpty(session.JoinCode))
                {
                    session.JoinCode = GenerateJoinCode();
                }
                
                _context.PuzzleSessions.Add(session);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created session {SessionId} for puzzle {PuzzleId}", 
                    session.Id, session.PuzzleId);
                
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session for puzzle {PuzzleId}", session.PuzzleId);
                throw;
            }
        }

        public async Task<PuzzleSession?> GetSessionByIdAsync(Guid sessionId)
        {
            // Legacy method support - delegates to GetSessionAsync
            return await GetSessionAsync(sessionId);
        }

        public async Task<PuzzleSession?> GetSessionByJoinCodeAsync(string joinCode)
        {
            return await _context.PuzzleSessions
                .Include(s => s.Puzzle)
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.JoinCode == joinCode && s.Status == SessionStatus.Active);
        }

        public async Task<PuzzleSession?> GetSessionWithParticipantsAsync(Guid sessionId)
        {
            return await _context.PuzzleSessions
                .Include(s => s.Puzzle)
                .Include(s => s.CreatedByUser)
                .Include(s => s.Participants)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<IEnumerable<PuzzleSession>> GetActiveSessionsForPuzzleAsync(Guid puzzleId)
        {
            return await _context.PuzzleSessions
                .Where(s => s.PuzzleId == puzzleId && s.Status == SessionStatus.Active)
                .Include(s => s.CreatedByUser)
                .OrderByDescending(s => s.LastActivityAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<PuzzleSession>> GetPublicSessionsAsync(int skip, int take)
        {
            return await _context.PuzzleSessions
                .Where(s => s.IsPublic && s.Status == SessionStatus.Active)
                .Include(s => s.Puzzle)
                .Include(s => s.CreatedByUser)
                .OrderByDescending(s => s.LastActivityAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> UpdateSessionAsync(PuzzleSession session)
        {
            try
            {
                _context.PuzzleSessions.Update(session);
                session.LastActivityAt = DateTime.UtcNow;
                
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating session {SessionId}", session.Id);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task<bool> UpdateSessionProgressAsync(Guid sessionId, int completedPieces, decimal completionPercentage)
        {
            try
            {
                var session = await _context.PuzzleSessions.FindAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Session {SessionId} not found for progress update", sessionId);
                    return false;
                }

                session.CompletedPieces = completedPieces;
                session.CompletionPercentage = completionPercentage;
                session.LastActivityAt = DateTime.UtcNow;

                if (completionPercentage >= 100)
                {
                    session.Status = SessionStatus.Completed;
                    session.CompletedAt = DateTime.UtcNow;
                }

                var result = await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated progress for session {SessionId}: {CompletedPieces} pieces ({Percentage}%)",
                    sessionId, completedPieces, completionPercentage);
                
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating progress for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> DeleteSessionAsync(Guid sessionId)
        {
            try
            {
                var session = await _context.PuzzleSessions
                    .Include(s => s.Participants)
                    .Include(s => s.ChatMessages)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    return false;
                }

                // Remove related entities first
                _context.SessionParticipants.RemoveRange(session.Participants);
                _context.ChatMessages.RemoveRange(session.ChatMessages);
                _context.PuzzleSessions.Remove(session);

                var result = await _context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted session {SessionId}", sessionId);
                
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status)
        {
            var session = await _context.PuzzleSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status = status;
                session.LastActivityAt = DateTime.UtcNow;
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            return false;
        }

        public async Task<IEnumerable<PuzzleSession>> GetActiveSessionsAsync(int limit = 10)
        {
            return await _context.PuzzleSessions
                .Where(s => s.Status == SessionStatus.Active)
                .Include(s => s.Puzzle)
                .Include(s => s.CreatedByUser)
                .OrderByDescending(s => s.LastActivityAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<SessionParticipant> AddParticipantAsync(Guid sessionId, Guid userId, string? connectionId = null)
        {
            try
            {
                // Check if participant already exists
                var existing = await _context.SessionParticipants
                    .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);

                if (existing != null)
                {
                    // Update connection ID if provided
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        existing.ConnectionId = connectionId;
                        existing.Status = ParticipantStatus.Online;
                        await _context.SaveChangesAsync();
                    }
                    return existing;
                }

                var participant = new SessionParticipant
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    ConnectionId = connectionId,
                    JoinedAt = DateTime.UtcNow,
                    Status = ParticipantStatus.Online
                };

                _context.SessionParticipants.Add(participant);
                
                // Update session activity
                var session = await _context.PuzzleSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
                
                return participant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding participant {UserId} to session {SessionId}", userId, sessionId);
                throw;
            }
        }

        public async Task<bool> RemoveParticipantAsync(Guid sessionId, Guid userId)
        {
            try
            {
                var participant = await _context.SessionParticipants
                    .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);

                if (participant == null)
                {
                    return false;
                }

                _context.SessionParticipants.Remove(participant);
                
                // Update session activity
                var session = await _context.PuzzleSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.UtcNow;
                }

                var result = await _context.SaveChangesAsync();
                
                _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);
                
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing participant {UserId} from session {SessionId}", userId, sessionId);
                throw;
            }
        }

        public async Task<bool> UpdateParticipantStatusAsync(Guid sessionId, Guid userId, ParticipantStatus status)
        {
            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
            
            if (participant != null)
            {
                participant.Status = status;
                var result = await _context.SaveChangesAsync();
                return result > 0;
            }
            return false;
        }

        public async Task<IEnumerable<SessionParticipant>> GetSessionParticipantsAsync(Guid sessionId)
        {
            return await _context.SessionParticipants
                .Where(p => p.SessionId == sessionId)
                .Include(p => p.User)
                .OrderBy(p => p.JoinedAt)
                .ToListAsync();
        }

        public async Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, Guid userId)
        {
            return await _context.SessionParticipants
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
        }

        public async Task<ChatMessage> SaveChatMessageAsync(Guid sessionId, Guid userId, string message, MessageType messageType)
        {
            try
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

                _context.ChatMessages.Add(chatMessage);
                
                // Update session activity
                var session = await _context.PuzzleSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.LastActivityAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Saved {MessageType} message in session {SessionId}", messageType, sessionId);
                
                return chatMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat message in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> CompleteSessionAsync(Guid sessionId)
        {
            try
            {
                var session = await _context.PuzzleSessions.FindAsync(sessionId);
                if (session == null)
                {
                    return false;
                }

                session.Status = SessionStatus.Completed;
                session.CompletedAt = DateTime.UtcNow;
                session.CompletionPercentage = 100;
                session.LastActivityAt = DateTime.UtcNow;

                var result = await _context.SaveChangesAsync();
                
                _logger.LogInformation("Completed session {SessionId}", sessionId);
                
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing session {SessionId}", sessionId);
                throw;
            }
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