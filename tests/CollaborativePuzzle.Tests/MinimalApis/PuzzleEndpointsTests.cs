using System.Net;
using System.Net.Http.Json;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.MinimalApis;

public class PuzzleEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IPuzzleRepository> _mockPuzzleRepo;
    private readonly Mock<ISessionRepository> _mockSessionRepo;

    public PuzzleEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _mockPuzzleRepo = new Mock<IPuzzleRepository>();
        _mockSessionRepo = new Mock<ISessionRepository>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing services
                var puzzleRepoDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPuzzleRepository));
                if (puzzleRepoDescriptor != null) services.Remove(puzzleRepoDescriptor);
                
                var sessionRepoDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISessionRepository));
                if (sessionRepoDescriptor != null) services.Remove(sessionRepoDescriptor);
                
                // Add mocks
                services.AddSingleton(_mockPuzzleRepo.Object);
                services.AddSingleton(_mockSessionRepo.Object);
            });
        });
    }

    [Fact]
    public async Task GetPuzzles_ReturnsOkWithPuzzleList()
    {
        // Arrange
        var puzzles = new List<Puzzle>
        {
            new Puzzle { Id = Guid.NewGuid(), Title = "Test Puzzle 1", IsPublic = true },
            new Puzzle { Id = Guid.NewGuid(), Title = "Test Puzzle 2", IsPublic = true }
        };
        
        _mockPuzzleRepo.Setup(x => x.GetPublicPuzzlesAsync(0, 20, null, null))
            .ReturnsAsync(puzzles);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/v1/puzzles");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PuzzleListResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Puzzles.Count());
    }

    [Fact]
    public async Task GetPuzzleById_WithValidId_ReturnsOk()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var puzzle = new Puzzle 
        { 
            Id = puzzleId, 
            Title = "Test Puzzle",
            PieceCount = 100,
            IsPublic = true
        };
        
        _mockPuzzleRepo.Setup(x => x.GetPuzzleByIdAsync(puzzleId))
            .ReturnsAsync(puzzle);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/puzzles/{puzzleId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PuzzleDto>();
        Assert.NotNull(result);
        Assert.Equal(puzzle.Title, result.Title);
    }

    [Fact]
    public async Task GetPuzzleById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        _mockPuzzleRepo.Setup(x => x.GetPuzzleByIdAsync(puzzleId))
            .ReturnsAsync((Puzzle?)null);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/puzzles/{puzzleId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePuzzle_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreatePuzzleRequest
        {
            Title = "New Puzzle",
            Description = "Test Description",
            PieceCount = 100,
            Difficulty = "Medium",
            ImageUrl = "https://example.com/image.jpg"
        };
        
        var createdPuzzle = new Puzzle
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            PieceCount = request.PieceCount
        };
        
        _mockPuzzleRepo.Setup(x => x.CreatePuzzleAsync(It.IsAny<Puzzle>(), It.IsAny<IEnumerable<PuzzlePiece>>()))
            .ReturnsAsync(createdPuzzle);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/puzzles", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains($"/api/v1/puzzles/{createdPuzzle.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task UpdatePuzzle_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var existingPuzzle = new Puzzle 
        { 
            Id = puzzleId, 
            Title = "Old Title",
            CreatedByUserId = Guid.NewGuid()
        };
        
        var request = new UpdatePuzzleRequest
        {
            Title = "Updated Title",
            Description = "Updated Description"
        };
        
        _mockPuzzleRepo.Setup(x => x.GetPuzzleByIdAsync(puzzleId))
            .ReturnsAsync(existingPuzzle);
        _mockPuzzleRepo.Setup(x => x.UpdatePuzzleAsync(It.IsAny<Puzzle>()))
            .ReturnsAsync(true);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/puzzles/{puzzleId}", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeletePuzzle_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var puzzle = new Puzzle 
        { 
            Id = puzzleId,
            CreatedByUserId = Guid.NewGuid()
        };
        
        _mockPuzzleRepo.Setup(x => x.GetPuzzleByIdAsync(puzzleId))
            .ReturnsAsync(puzzle);
        _mockPuzzleRepo.Setup(x => x.DeletePuzzleAsync(puzzleId))
            .ReturnsAsync(true);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.DeleteAsync($"/api/v1/puzzles/{puzzleId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetPuzzleSessions_ReturnsOk()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var sessions = new List<PuzzleSession>
        {
            new PuzzleSession { Id = Guid.NewGuid(), PuzzleId = puzzleId, IsPublic = true }
        };
        
        _mockSessionRepo.Setup(x => x.GetActiveSessionsForPuzzleAsync(puzzleId))
            .ReturnsAsync(sessions);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/puzzles/{puzzleId}/sessions");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}