using System;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Tests.Helpers;
using CollaborativePuzzle.Tests.TestBase;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CollaborativePuzzle.Tests.Core.Services
{
    /// <summary>
    /// Unit tests for PuzzleService demonstrating TDD approach.
    /// Tests are written before the implementation to drive the design.
    /// </summary>
    public class PuzzleServiceTests : TestBase
    {
        private readonly Mock<IPuzzleRepository> _puzzleRepositoryMock;
        private readonly Mock<IBlobStorageService> _blobStorageMock;
        private readonly Mock<IRedisService> _redisMock;
        
        // System under test will be created once we implement it
        // private readonly PuzzleService _sut;

        public PuzzleServiceTests(ITestOutputHelper output) : base(output)
        {
            // Arrange: Create mocks for dependencies
            _puzzleRepositoryMock = CreateMock<IPuzzleRepository>();
            _blobStorageMock = CreateMock<IBlobStorageService>();
            _redisMock = CreateMock<IRedisService>();
            
            // Will create service once implemented
            // _sut = new PuzzleService(
            //     _puzzleRepositoryMock.Object,
            //     _blobStorageMock.Object,
            //     _redisMock.Object,
            //     GetService<ILogger<PuzzleService>>()
            // );
        }

        [Fact]
        public async Task CreatePuzzleAsync_WithValidData_ShouldCreatePuzzleSuccessfully()
        {
            // This test is written first to drive the implementation
            LogTestStep("Testing puzzle creation with valid data");
            
            // Arrange
            var userId = Guid.NewGuid();
            var puzzleTitle = "Test Puzzle";
            var imageData = new byte[] { 1, 2, 3, 4, 5 };
            var pieceCount = 100;
            
            var expectedPuzzle = TestDataBuilder.Puzzle()
                .WithTitle(puzzleTitle)
                .WithPieceCount(pieceCount)
                .WithCreator(userId)
                .Build();
            
            var imageUrl = "https://storage.example.com/puzzles/test-puzzle.jpg";
            var piecesDataUrl = "https://storage.example.com/puzzles/test-puzzle-pieces.json";
            
            // Setup mock expectations
            _blobStorageMock
                .Setup(x => x.UploadImageAsync(It.IsAny<string>(), imageData, It.IsAny<string>()))
                .ReturnsAsync(imageUrl);
            
            _blobStorageMock
                .Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(piecesDataUrl);
            
            _puzzleRepositoryMock
                .Setup(x => x.CreatePuzzleAsync(It.IsAny<Puzzle>()))
                .ReturnsAsync((Puzzle p) => 
                {
                    p.Id = expectedPuzzle.Id;
                    return p;
                });
            
            // Act
            // var result = await _sut.CreatePuzzleAsync(userId, puzzleTitle, imageData, pieceCount);
            
            // Assert (what we expect the implementation to do)
            // result.Should().NotBeNull();
            // result.Id.Should().Be(expectedPuzzle.Id);
            // result.Title.Should().Be(puzzleTitle);
            // result.PieceCount.Should().Be(pieceCount);
            // result.CreatedByUserId.Should().Be(userId);
            // result.ImageUrl.Should().Be(imageUrl);
            // result.PiecesDataUrl.Should().Be(piecesDataUrl);
            
            // Verify all mocks were called correctly
            _blobStorageMock.Verify(x => x.UploadImageAsync(
                It.Is<string>(s => s.Contains("puzzles")),
                imageData,
                It.IsAny<string>()
            ), Times.Once);
            
            _puzzleRepositoryMock.Verify(x => x.CreatePuzzleAsync(
                It.Is<Puzzle>(p => 
                    p.Title == puzzleTitle && 
                    p.PieceCount == pieceCount &&
                    p.CreatedByUserId == userId
                )
            ), Times.Once);
        }

        [Fact]
        public async Task GetPuzzleWithPiecesAsync_WhenCached_ShouldReturnFromCache()
        {
            LogTestStep("Testing puzzle retrieval from cache");
            
            // Arrange
            var puzzleId = Guid.NewGuid();
            var cachedPuzzle = TestDataBuilder.Puzzle()
                .WithId(puzzleId)
                .WithPieces(100)
                .Build();
            
            _redisMock
                .Setup(x => x.GetAsync<Puzzle>($"puzzle:{puzzleId}"))
                .ReturnsAsync(cachedPuzzle);
            
            // Act
            // var result = await _sut.GetPuzzleWithPiecesAsync(puzzleId);
            
            // Assert
            // result.Should().NotBeNull();
            // result.Should().BeEquivalentTo(cachedPuzzle);
            
            // Should not hit the database
            _puzzleRepositoryMock.Verify(x => x.GetPuzzleWithPiecesAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetPuzzleWithPiecesAsync_WhenNotCached_ShouldFetchFromDatabaseAndCache()
        {
            LogTestStep("Testing puzzle retrieval from database with caching");
            
            // Arrange
            var puzzleId = Guid.NewGuid();
            var dbPuzzle = TestDataBuilder.Puzzle()
                .WithId(puzzleId)
                .WithPieces(100)
                .Build();
            
            _redisMock
                .Setup(x => x.GetAsync<Puzzle>($"puzzle:{puzzleId}"))
                .ReturnsAsync((Puzzle?)null);
            
            _puzzleRepositoryMock
                .Setup(x => x.GetPuzzleWithPiecesAsync(puzzleId))
                .ReturnsAsync(dbPuzzle);
            
            _redisMock
                .Setup(x => x.SetAsync($"puzzle:{puzzleId}", dbPuzzle, It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);
            
            // Act
            // var result = await _sut.GetPuzzleWithPiecesAsync(puzzleId);
            
            // Assert
            // result.Should().NotBeNull();
            // result.Should().BeEquivalentTo(dbPuzzle);
            
            // Verify caching occurred
            _redisMock.Verify(x => x.SetAsync(
                $"puzzle:{puzzleId}",
                dbPuzzle,
                It.Is<TimeSpan>(t => t.TotalMinutes >= 5)
            ), Times.Once);
        }

        [Theory]
        [InlineData(PuzzleDifficulty.Easy, 50)]
        [InlineData(PuzzleDifficulty.Medium, 100)]
        [InlineData(PuzzleDifficulty.Hard, 200)]
        [InlineData(PuzzleDifficulty.Expert, 500)]
        public async Task CreatePuzzleAsync_ShouldSetCorrectPieceCountBasedOnDifficulty(
            PuzzleDifficulty difficulty, 
            int expectedPieceCount)
        {
            LogTestStep($"Testing piece count for {difficulty} difficulty");
            
            // Arrange
            var userId = Guid.NewGuid();
            var imageData = new byte[] { 1, 2, 3 };
            
            _blobStorageMock
                .Setup(x => x.UploadImageAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("https://example.com/image.jpg");
            
            _blobStorageMock
                .Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync("https://example.com/pieces.json");
            
            _puzzleRepositoryMock
                .Setup(x => x.CreatePuzzleAsync(It.IsAny<Puzzle>()))
                .ReturnsAsync((Puzzle p) => p);
            
            // Act
            // var result = await _sut.CreatePuzzleAsync(userId, "Test", imageData, difficulty: difficulty);
            
            // Assert
            // result.PieceCount.Should().Be(expectedPieceCount);
            // result.Difficulty.Should().Be(difficulty);
        }

        [Fact]
        public async Task DeletePuzzleAsync_WhenUserIsOwner_ShouldDeleteSuccessfully()
        {
            LogTestStep("Testing puzzle deletion by owner");
            
            // Arrange
            var userId = Guid.NewGuid();
            var puzzleId = Guid.NewGuid();
            
            var puzzle = TestDataBuilder.Puzzle()
                .WithId(puzzleId)
                .WithCreator(userId)
                .Build();
            
            _puzzleRepositoryMock
                .Setup(x => x.GetPuzzleAsync(puzzleId))
                .ReturnsAsync(puzzle);
            
            _puzzleRepositoryMock
                .Setup(x => x.DeletePuzzleAsync(puzzleId))
                .Returns(Task.CompletedTask);
            
            _blobStorageMock
                .Setup(x => x.DeleteBlobAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            _redisMock
                .Setup(x => x.DeleteAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Act
            // await _sut.DeletePuzzleAsync(puzzleId, userId);
            
            // Assert - verify all cleanup occurred
            _puzzleRepositoryMock.Verify(x => x.DeletePuzzleAsync(puzzleId), Times.Once);
            _blobStorageMock.Verify(x => x.DeleteBlobAsync(It.IsAny<string>()), Times.AtLeastOnce);
            _redisMock.Verify(x => x.DeleteAsync($"puzzle:{puzzleId}"), Times.Once);
        }

        [Fact]
        public async Task DeletePuzzleAsync_WhenUserIsNotOwner_ShouldThrowUnauthorizedException()
        {
            LogTestStep("Testing puzzle deletion by non-owner");
            
            // Arrange
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var puzzleId = Guid.NewGuid();
            
            var puzzle = TestDataBuilder.Puzzle()
                .WithId(puzzleId)
                .WithCreator(ownerId)
                .Build();
            
            _puzzleRepositoryMock
                .Setup(x => x.GetPuzzleAsync(puzzleId))
                .ReturnsAsync(puzzle);
            
            // Act & Assert
            // var exception = await CaptureExceptionAsync(async () => 
            //     await _sut.DeletePuzzleAsync(puzzleId, otherUserId)
            // );
            
            // exception.Should().NotBeNull();
            // exception.Should().BeOfType<UnauthorizedException>();
            // exception.Message.Should().Contain("not authorized");
            
            // Should not delete anything
            _puzzleRepositoryMock.Verify(x => x.DeletePuzzleAsync(It.IsAny<Guid>()), Times.Never);
            _blobStorageMock.Verify(x => x.DeleteBlobAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GeneratePuzzlePieces_ShouldCreateCorrectNumberOfPieces()
        {
            LogTestStep("Testing puzzle piece generation");
            
            // Arrange
            var imageWidth = 1000;
            var imageHeight = 800;
            var pieceCount = 100; // Should create 10x10 grid
            
            // Act
            // var pieces = await _sut.GeneratePuzzlePiecesAsync(imageWidth, imageHeight, pieceCount);
            
            // Assert
            // pieces.Should().NotBeNull();
            // pieces.Should().HaveCount(pieceCount);
            
            // All pieces should have unique positions
            // pieces.Select(p => new { p.CorrectX, p.CorrectY })
            //     .Should().OnlyHaveUniqueItems();
            
            // Pieces should cover the entire image
            // pieces.Max(p => p.CorrectX).Should().BeApproximately(imageWidth, 100);
            // pieces.Max(p => p.CorrectY).Should().BeApproximately(imageHeight, 100);
        }
    }
}