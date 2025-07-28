using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace CollaborativePuzzle.Infrastructure.Services
{
    public class PuzzleGeneratorService : IPuzzleGeneratorService
    {
        private readonly ILogger<PuzzleGeneratorService> _logger;

        public PuzzleGeneratorService(ILogger<PuzzleGeneratorService> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<PuzzlePiece>> GeneratePuzzlePiecesAsync(
            string imageUrl, 
            int pieceCount, 
            Guid puzzleId,
            int width,
            int height)
        {
            _logger.LogInformation("Generating {PieceCount} pieces for puzzle {PuzzleId}", pieceCount, puzzleId);

            var aspectRatio = (double)width / height;
            var (columns, rows) = CalculateGridDimensions(pieceCount, aspectRatio);
            
            var pieces = new List<PuzzlePiece>();
            var pieceWidth = width / columns;
            var pieceHeight = height / rows;
            var pieceNumber = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (pieceNumber >= pieceCount) break;

                    var isEdge = x == 0 || x == columns - 1 || y == 0 || y == rows - 1;
                    var isCorner = (x == 0 || x == columns - 1) && (y == 0 || y == rows - 1);

                    var piece = new PuzzlePiece
                    {
                        Id = Guid.NewGuid(),
                        PuzzleId = puzzleId,
                        PieceNumber = pieceNumber,
                        GridX = x,
                        GridY = y,
                        CorrectX = x * pieceWidth,
                        CorrectY = y * pieceHeight,
                        // Scatter pieces randomly around the board
                        CurrentX = RandomNumberGenerator.GetInt32(-100, width + 100),
                        CurrentY = RandomNumberGenerator.GetInt32(-100, height + 100),
                        Rotation = 0,
                        ShapeData = GeneratePieceShape(x, y, columns, rows, pieceWidth, pieceHeight),
                        ImageX = x * pieceWidth,
                        ImageY = y * pieceHeight,
                        ImageWidth = pieceWidth,
                        ImageHeight = pieceHeight,
                        IsPlaced = false,
                        IsEdgePiece = isEdge,
                        IsCornerPiece = isCorner,
                        CreatedAt = DateTime.UtcNow
                    };

                    pieces.Add(piece);
                    pieceNumber++;
                }
            }

            _logger.LogInformation("Generated {Count} pieces with {Columns}x{Rows} grid", pieces.Count, columns, rows);
            
            // Simulate async operation (in real implementation, might download/process image)
            await Task.Delay(10);
            
            return pieces;
        }

        public (int columns, int rows) CalculateGridDimensions(int pieceCount, double aspectRatio)
        {
            // Start with square root as baseline
            var sqrt = Math.Sqrt(pieceCount);
            
            // Adjust for aspect ratio
            var columns = (int)Math.Ceiling(sqrt * Math.Sqrt(aspectRatio));
            var rows = (int)Math.Ceiling(pieceCount / (double)columns);
            
            // Ensure we have enough cells
            while (columns * rows < pieceCount)
            {
                if (aspectRatio > 1)
                    columns++;
                else
                    rows++;
            }
            
            return (columns, rows);
        }

        public string GeneratePieceShape(
            int gridX, 
            int gridY, 
            int totalColumns, 
            int totalRows,
            int pieceWidth,
            int pieceHeight)
        {
            // Generate a basic rectangular shape with puzzle piece tabs/blanks
            var path = new System.Text.StringBuilder();
            
            // Start at top-left corner
            path.Append($"M 0 0 ");
            
            // Top edge
            if (gridY > 0)
            {
                // Add tab or blank for interlocking
                path.Append($"L {pieceWidth * 0.4} 0 ");
                path.Append($"C {pieceWidth * 0.4} {-pieceHeight * 0.1} {pieceWidth * 0.6} {-pieceHeight * 0.1} {pieceWidth * 0.6} 0 ");
                path.Append($"L {pieceWidth} 0 ");
            }
            else
            {
                // Straight edge for border pieces
                path.Append($"L {pieceWidth} 0 ");
            }
            
            // Right edge
            if (gridX < totalColumns - 1)
            {
                // Add tab or blank
                path.Append($"L {pieceWidth} {pieceHeight * 0.4} ");
                path.Append($"C {pieceWidth + pieceWidth * 0.1} {pieceHeight * 0.4} {pieceWidth + pieceWidth * 0.1} {pieceHeight * 0.6} {pieceWidth} {pieceHeight * 0.6} ");
                path.Append($"L {pieceWidth} {pieceHeight} ");
            }
            else
            {
                // Straight edge
                path.Append($"L {pieceWidth} {pieceHeight} ");
            }
            
            // Bottom edge
            if (gridY < totalRows - 1)
            {
                // Add tab or blank
                path.Append($"L {pieceWidth * 0.6} {pieceHeight} ");
                path.Append($"C {pieceWidth * 0.6} {pieceHeight + pieceHeight * 0.1} {pieceWidth * 0.4} {pieceHeight + pieceHeight * 0.1} {pieceWidth * 0.4} {pieceHeight} ");
                path.Append($"L 0 {pieceHeight} ");
            }
            else
            {
                // Straight edge
                path.Append($"L 0 {pieceHeight} ");
            }
            
            // Left edge
            if (gridX > 0)
            {
                // Add tab or blank
                path.Append($"L 0 {pieceHeight * 0.6} ");
                path.Append($"C {-pieceWidth * 0.1} {pieceHeight * 0.6} {-pieceWidth * 0.1} {pieceHeight * 0.4} 0 {pieceHeight * 0.4} ");
            }
            
            // Close the path
            path.Append("Z");
            
            return path.ToString();
        }
    }
}