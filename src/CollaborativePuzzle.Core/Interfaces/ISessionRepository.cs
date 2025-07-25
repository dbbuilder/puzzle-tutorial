using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Repository interface for puzzle session data access operations
    /// </summary>
    public interface ISessionRepository
    {
        /// <summary>
        /// Creates a new puzzle session
        /// </summary>
        /// <param name="session">Session entity to create</param>
        /// <returns>Created session with assigned ID</returns>
        Task<PuzzleSession> CreateSessionAsync(PuzzleSession session);
        
        /// <summary>
        /// Retrieves a session by its unique identifier
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <returns>Session entity or null if not found</returns>
        Task<PuzzleSession?> GetSessionByIdAsync(Guid sessionId);
        
        /// <summary>
        /// Retrieves a session by its join code
        /// </summary>
        /// <param name="joinCode">Session join code</param>
        /// <returns>Session entity or null if not found</returns>
        Task<PuzzleSession?> GetSessionByJoinCodeAsync(string joinCode);
        
        /// <summary>
        /// Retrieves a session with all participants
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <returns>Session entity with participants or null if not found</returns>
        Task<PuzzleSession?> GetSessionWithParticipantsAsync(Guid sessionId);
        
        /// <summary>
        /// Retrieves active sessions for a specific puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>List of active sessions</returns>
        Task<IEnumerable<PuzzleSession>> GetActiveSessionsForPuzzleAsync(Guid puzzleId);
        
        /// <summary>
        /// Retrieves public sessions with pagination
        /// </summary>
        /// <param name="skip">Number of records to skip</param>
        /// <param name="take">Number of records to take</param>
        /// <returns>List of public sessions</returns>
        Task<IEnumerable<PuzzleSession>> GetPublicSessionsAsync(int skip, int take);
        
        /// <summary>
        /// Updates session information
        /// </summary>
        /// <param name="session">Session entity with updated information</param>
        /// <returns>True if update was successful</returns>
        Task<bool> UpdateSessionAsync(PuzzleSession session);
        
        /// <summary>
        /// Updates session progress information
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="completedPieces">Number of completed pieces</param>
        /// <param name="completionPercentage">Completion percentage</param>
        /// <returns>True if update was successful</returns>
        Task<bool> UpdateSessionProgressAsync(Guid sessionId, int completedPieces, decimal completionPercentage);
        
        /// <summary>
        /// Deletes a session and all associated data
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeleteSessionAsync(Guid sessionId);
    }
}
