using CollaborativePuzzle.Core.Entities;
using System.Threading.Tasks;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Service for generating puzzle pieces from an image
    /// </summary>
    public interface IPuzzleGeneratorService
    {
        /// <summary>
        /// Generates puzzle pieces from an image URL
        /// </summary>
        /// <param name="imageUrl">URL of the puzzle image</param>
        /// <param name="pieceCount">Total number of pieces to generate</param>
        /// <param name="puzzleId">ID of the puzzle</param>
        /// <param name="width">Width of the puzzle in pixels</param>
        /// <param name="height">Height of the puzzle in pixels</param>
        /// <returns>Collection of generated puzzle pieces</returns>
        Task<IEnumerable<PuzzlePiece>> GeneratePuzzlePiecesAsync(
            string imageUrl, 
            int pieceCount, 
            Guid puzzleId,
            int width,
            int height);

        /// <summary>
        /// Calculates optimal grid dimensions for a given piece count
        /// </summary>
        /// <param name="pieceCount">Total number of pieces</param>
        /// <param name="aspectRatio">Image aspect ratio (width/height)</param>
        /// <returns>Tuple of (columns, rows)</returns>
        (int columns, int rows) CalculateGridDimensions(int pieceCount, double aspectRatio);

        /// <summary>
        /// Generates SVG path data for a puzzle piece shape
        /// </summary>
        /// <param name="gridX">Grid X position</param>
        /// <param name="gridY">Grid Y position</param>
        /// <param name="totalColumns">Total columns in grid</param>
        /// <param name="totalRows">Total rows in grid</param>
        /// <param name="pieceWidth">Width of piece in pixels</param>
        /// <param name="pieceHeight">Height of piece in pixels</param>
        /// <returns>SVG path data as string</returns>
        string GeneratePieceShape(
            int gridX, 
            int gridY, 
            int totalColumns, 
            int totalRows,
            int pieceWidth,
            int pieceHeight);
    }
}