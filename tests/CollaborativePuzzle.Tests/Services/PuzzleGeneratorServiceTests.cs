using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Services
{
    public class PuzzleGeneratorServiceTests
    {
        private readonly Mock<ILogger<PuzzleGeneratorService>> _loggerMock;
        private readonly PuzzleGeneratorService _service;

        public PuzzleGeneratorServiceTests()
        {
            _loggerMock = new Mock<ILogger<PuzzleGeneratorService>>();
            _service = new PuzzleGeneratorService(_loggerMock.Object);
        }

        [Theory]
        [InlineData(12, 1.5, 4, 3)]  // 12 pieces, 1.5 aspect ratio -> 4x3
        [InlineData(20, 1.333, 5, 4)] // 20 pieces, 4:3 aspect ratio -> 5x4
        [InlineData(100, 1.0, 10, 10)] // 100 pieces, square -> 10x10
        [InlineData(48, 1.777, 8, 6)]  // 48 pieces, 16:9 aspect ratio -> 8x6
        [InlineData(15, 1.5, 5, 3)]    // 15 pieces, 1.5 aspect ratio -> 5x3
        public void CalculateGridDimensions_ShouldReturnOptimalGrid(
            int pieceCount, double aspectRatio, int expectedColumns, int expectedRows)
        {
            // Act
            var (columns, rows) = _service.CalculateGridDimensions(pieceCount, aspectRatio);

            // Assert
            columns.Should().Be(expectedColumns);
            rows.Should().Be(expectedRows);
            (columns * rows).Should().BeGreaterThanOrEqualTo(pieceCount);
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldCreateCorrectNumberOfPieces()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12;
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height);

            // Assert
            pieces.Should().HaveCount(12);
            pieces.All(p => p.PuzzleId == puzzleId).Should().BeTrue();
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldSetCorrectPieceProperties()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12;
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = (await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height)).ToList();

            // Assert
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                piece.Id.Should().NotBeEmpty();
                piece.PieceNumber.Should().Be(i);
                piece.ShapeData.Should().NotBeNullOrEmpty();
                piece.ImageWidth.Should().BeGreaterThan(0);
                piece.ImageHeight.Should().BeGreaterThan(0);
                piece.Rotation.Should().Be(0);
                piece.IsPlaced.Should().BeFalse();
                piece.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldIdentifyEdgeAndCornerPieces()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12; // Will be 4x3 grid
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = (await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height)).ToList();

            // Assert
            // Corner pieces (4 total)
            var cornerPieces = pieces.Where(p => p.IsCornerPiece).ToList();
            cornerPieces.Should().HaveCount(4);

            // Edge pieces (excluding corners) - for 4x3 grid: 2*(4-2) + 2*(3-2) = 4 + 2 = 6
            var edgePieces = pieces.Where(p => p.IsEdgePiece && !p.IsCornerPiece).ToList();
            edgePieces.Should().HaveCount(6);

            // Center pieces - for 4x3 grid: (4-2)*(3-2) = 2*1 = 2
            var centerPieces = pieces.Where(p => !p.IsEdgePiece).ToList();
            centerPieces.Should().HaveCount(2);
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldSetCorrectGridPositions()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12; // Will be 4x3 grid
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = (await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height)).ToList();

            // Assert
            // Check that all grid positions are unique
            var gridPositions = pieces.Select(p => (p.GridX, p.GridY)).ToList();
            gridPositions.Should().OnlyHaveUniqueItems();

            // Check grid boundaries
            pieces.All(p => p.GridX >= 0 && p.GridX < 4).Should().BeTrue();
            pieces.All(p => p.GridY >= 0 && p.GridY < 3).Should().BeTrue();
        }

        [Theory]
        [InlineData(0, 0, 4, 3, true, true)]   // Top-left corner
        [InlineData(3, 0, 4, 3, true, true)]   // Top-right corner
        [InlineData(0, 2, 4, 3, true, true)]   // Bottom-left corner
        [InlineData(3, 2, 4, 3, true, true)]   // Bottom-right corner
        [InlineData(1, 0, 4, 3, true, false)]  // Top edge
        [InlineData(0, 1, 4, 3, true, false)]  // Left edge
        [InlineData(1, 1, 4, 3, false, false)] // Center piece
        public void GeneratePieceShape_ShouldCreateValidSVGPath(
            int gridX, int gridY, int totalColumns, int totalRows, 
            bool expectedIsEdge, bool expectedIsCorner)
        {
            // Arrange
            var pieceWidth = 200;
            var pieceHeight = 200;

            // Act
            var svgPath = _service.GeneratePieceShape(gridX, gridY, totalColumns, totalRows, pieceWidth, pieceHeight);

            // Assert
            svgPath.Should().NotBeNullOrEmpty();
            svgPath.Should().StartWith("M"); // SVG path should start with Move command
            svgPath.Should().Contain("L");   // Should contain Line commands
            svgPath.Should().EndWith("Z");   // Should close the path
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldSetCorrectImageCoordinates()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12; // Will be 4x3 grid
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = (await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height)).ToList();

            // Assert
            var pieceWidth = width / 4;
            var pieceHeight = height / 3;

            foreach (var piece in pieces)
            {
                piece.ImageX.Should().Be(piece.GridX * pieceWidth);
                piece.ImageY.Should().Be(piece.GridY * pieceHeight);
                piece.ImageWidth.Should().Be(pieceWidth);
                piece.ImageHeight.Should().Be(pieceHeight);
                piece.CorrectX.Should().Be(piece.ImageX);
                piece.CorrectY.Should().Be(piece.ImageY);
            }
        }

        [Fact]
        public async Task GeneratePuzzlePiecesAsync_ShouldScatterInitialPositions()
        {
            // Arrange
            var imageUrl = "https://example.com/puzzle.jpg";
            var pieceCount = 12;
            var puzzleId = Guid.NewGuid();
            var width = 800;
            var height = 600;

            // Act
            var pieces = (await _service.GeneratePuzzlePiecesAsync(imageUrl, pieceCount, puzzleId, width, height)).ToList();

            // Assert
            // Initial positions should be scattered (not at correct positions)
            pieces.Any(p => p.CurrentX != p.CorrectX || p.CurrentY != p.CorrectY).Should().BeTrue();
            
            // But should be within reasonable bounds
            pieces.All(p => p.CurrentX >= -100 && p.CurrentX <= width + 100).Should().BeTrue();
            pieces.All(p => p.CurrentY >= -100 && p.CurrentY <= height + 100).Should().BeTrue();
        }
    }
}