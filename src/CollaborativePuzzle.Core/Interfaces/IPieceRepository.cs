using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Repository interface for puzzle piece data access operations
    /// </summary>
    public interface IPieceRepository
    {
        /// <summary>
        /// Gets a specific puzzle piece by ID
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <returns>Puzzle piece entity or null if not found</returns>
        Task<PuzzlePiece?> GetPieceAsync(Guid pieceId);
        
        /// <summary>
        /// Gets all pieces for a specific puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>List of puzzle pieces</returns>
        Task<IEnumerable<PuzzlePiece>> GetPuzzlePiecesAsync(Guid puzzleId);
        
        /// <summary>
        /// Updates the position of a puzzle piece
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <param name="x">New X coordinate</param>
        /// <param name="y">New Y coordinate</param>
        /// <param name="rotation">New rotation angle</param>
        /// <returns>True if update was successful</returns>
        Task<bool> UpdatePiecePositionAsync(Guid pieceId, double x, double y, int rotation);
        
        /// <summary>
        /// Locks a piece for exclusive editing by a user
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <param name="userId">User identifier</param>
        /// <returns>True if lock was acquired successfully</returns>
        Task<bool> LockPieceAsync(Guid pieceId, Guid userId);
        
        /// <summary>
        /// Unlocks a piece
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <returns>True if unlock was successful</returns>
        Task<bool> UnlockPieceAsync(Guid pieceId);
        
        /// <summary>
        /// Unlocks all pieces held by a specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <returns>Number of pieces unlocked</returns>
        Task<int> UnlockAllPiecesForUserAsync(Guid userId);
        
        /// <summary>
        /// Unlocks all pieces held by a specific user (legacy method name)
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <returns>Number of pieces unlocked</returns>
        Task<int> UnlockAllPiecesByUserAsync(Guid userId);
        
        /// <summary>
        /// Marks a piece as correctly placed
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <returns>True if marking was successful</returns>
        Task<bool> MarkPieceAsPlacedAsync(Guid pieceId);
        
        /// <summary>
        /// Gets the count of correctly placed pieces for a puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Number of placed pieces</returns>
        Task<int> GetPlacedPieceCountAsync(Guid puzzleId);
        
        /// <summary>
        /// Gets the total number of pieces in a puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Total piece count</returns>
        Task<int> GetTotalPieceCountAsync(Guid puzzleId);
        
        /// <summary>
        /// Gets the current progress statistics for a puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Progress statistics including completion percentage</returns>
        Task<(int CompletedPieces, decimal CompletionPercentage)> GetPuzzleProgressAsync(Guid puzzleId);
        
        /// <summary>
        /// Creates multiple puzzle pieces in a batch
        /// </summary>
        /// <param name="pieces">Collection of pieces to create</param>
        /// <returns>True if creation was successful</returns>
        Task<bool> CreatePiecesAsync(IEnumerable<PuzzlePiece> pieces);
        
        /// <summary>
        /// Deletes all pieces for a specific puzzle
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Number of pieces deleted</returns>
        Task<int> DeletePuzzlePiecesAsync(Guid puzzleId);
    }
}