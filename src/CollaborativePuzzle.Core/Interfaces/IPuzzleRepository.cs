using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Repository interface for puzzle data access operations
    /// </summary>
    public interface IPuzzleRepository
    {
        /// <summary>
        /// Creates a new puzzle with generated pieces
        /// </summary>
        /// <param name="puzzle">Puzzle entity to create</param>
        /// <param name="pieces">List of puzzle pieces</param>
        /// <returns>Created puzzle with assigned ID</returns>
        Task<Puzzle> CreatePuzzleAsync(Puzzle puzzle, IEnumerable<PuzzlePiece> pieces);
        
        /// <summary>
        /// Retrieves a puzzle by its unique identifier
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Puzzle entity or null if not found</returns>
        Task<Puzzle?> GetPuzzleByIdAsync(Guid puzzleId);
        
        /// <summary>
        /// Retrieves a puzzle with all its pieces
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Puzzle entity with pieces or null if not found</returns>
        Task<Puzzle?> GetPuzzleWithPiecesAsync(Guid puzzleId);
        
        /// <summary>
        /// Retrieves all public puzzles with pagination
        /// </summary>
        /// <param name="skip">Number of records to skip</param>
        /// <param name="take">Number of records to take</param>
        /// <param name="category">Optional category filter</param>
        /// <param name="difficulty">Optional difficulty filter</param>
        /// <returns>List of puzzles</returns>
        Task<IEnumerable<Puzzle>> GetPublicPuzzlesAsync(int skip, int take, string? category = null, string? difficulty = null);
        
        /// <summary>
        /// Retrieves puzzles created by a specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="skip">Number of records to skip</param>
        /// <param name="take">Number of records to take</param>
        /// <returns>List of puzzles created by the user</returns>
        Task<IEnumerable<Puzzle>> GetPuzzlesByUserAsync(Guid userId, int skip, int take);
        
        /// <summary>
        /// Updates puzzle metadata
        /// </summary>
        /// <param name="puzzle">Puzzle entity with updated information</param>
        /// <returns>True if update was successful</returns>
        Task<bool> UpdatePuzzleAsync(Puzzle puzzle);
        
        /// <summary>
        /// Deletes a puzzle and all associated data
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeletePuzzleAsync(Guid puzzleId);
        
        /// <summary>
        /// Searches puzzles by title and description
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <param name="skip">Number of records to skip</param>
        /// <param name="take">Number of records to take</param>
        /// <returns>List of matching puzzles</returns>
        Task<IEnumerable<Puzzle>> SearchPuzzlesAsync(string searchTerm, int skip, int take);
    }
}
