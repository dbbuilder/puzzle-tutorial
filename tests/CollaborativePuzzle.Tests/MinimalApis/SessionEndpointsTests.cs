using System.Net;
using System.Net.Http.Json;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.MinimalApis;

public class SessionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IPuzzleRepository> _mockPuzzleRepo;

    public SessionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockPuzzleRepo = new Mock<IPuzzleRepository>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing services
                var sessionRepoDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISessionRepository));
                if (sessionRepoDescriptor != null) services.Remove(sessionRepoDescriptor);
                
                var puzzleRepoDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPuzzleRepository));
                if (puzzleRepoDescriptor != null) services.Remove(puzzleRepoDescriptor);
                
                // Add mocks
                services.AddSingleton(_mockSessionRepo.Object);
                services.AddSingleton(_mockPuzzleRepo.Object);
            });
        });
    }

    [Fact]
    public async Task GetPublicSessions_ReturnsOkWithSessionList()
    {
        // Arrange
        var sessions = new List<PuzzleSession>
        {
            new PuzzleSession { Id = Guid.NewGuid(), Name = "Session 1", IsPublic = true },
            new PuzzleSession { Id = Guid.NewGuid(), Name = "Session 2", IsPublic = true }
        };
        
        _mockSessionRepo.Setup(x => x.GetPublicSessionsAsync(0, 20))
            .ReturnsAsync(sessions);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/api/v1/sessions");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionListResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Sessions.Count());
    }

    [Fact]
    public async Task GetSessionById_WithValidId_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new PuzzleSession 
        { 
            Id = sessionId, 
            Name = "Test Session",
            IsPublic = true,
            Puzzle = new Puzzle { Title = "Test Puzzle" }
        };
        
        _mockSessionRepo.Setup(x => x.GetSessionWithParticipantsAsync(sessionId))
            .ReturnsAsync(session);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/sessions/{sessionId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionDetailsDto>();
        Assert.NotNull(result);
        Assert.Equal(session.Name, result.Name);
    }

    [Fact]
    public async Task GetSessionById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionRepo.Setup(x => x.GetSessionWithParticipantsAsync(sessionId))
            .ReturnsAsync((PuzzleSession?)null);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/sessions/{sessionId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_WithValidData_ReturnsCreated()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var request = new CreateSessionRequest
        {
            PuzzleId = puzzleId,
            Name = "New Session",
            IsPublic = true,
            MaxParticipants = 4
        };
        
        var puzzle = new Puzzle
        {
            Id = puzzleId,
            Title = "Test Puzzle"
        };
        
        var createdSession = new PuzzleSession
        {
            Id = Guid.NewGuid(),
            PuzzleId = puzzleId,
            Name = request.Name,
            IsPublic = true,
            Puzzle = puzzle
        };
        
        _mockPuzzleRepo.Setup(x => x.GetPuzzleByIdAsync(puzzleId))
            .ReturnsAsync(puzzle);
        _mockSessionRepo.Setup(x => x.CreateSessionAsync(It.IsAny<PuzzleSession>()))
            .ReturnsAsync(createdSession);
        _mockSessionRepo.Setup(x => x.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/sessions", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains($"/api/v1/sessions/{createdSession.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task JoinSession_WithValidId_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new PuzzleSession 
        { 
            Id = sessionId,
            MaxParticipants = 4,
            CurrentParticipants = 1
        };
        
        _mockSessionRepo.Setup(x => x.GetSessionWithParticipantsAsync(sessionId))
            .ReturnsAsync(session);
        _mockSessionRepo.Setup(x => x.GetParticipantAsync(sessionId, It.IsAny<Guid>()))
            .ReturnsAsync((SessionParticipant?)null);
        _mockSessionRepo.Setup(x => x.AddParticipantAsync(sessionId, It.IsAny<Guid>()))
            .ReturnsAsync(true);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsync($"/api/v1/sessions/{sessionId}/join", null);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LeaveSession_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        
        _mockSessionRepo.Setup(x => x.RemoveParticipantAsync(sessionId, It.IsAny<Guid>()))
            .ReturnsAsync(true);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsync($"/api/v1/sessions/{sessionId}/leave", null);
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetSessionByJoinCode_WithValidCode_ReturnsOk()
    {
        // Arrange
        var joinCode = "ABC123";
        var session = new PuzzleSession 
        { 
            Id = Guid.NewGuid(),
            Name = "Test Session",
            JoinCode = joinCode,
            Puzzle = new Puzzle { Title = "Test Puzzle" }
        };
        
        _mockSessionRepo.Setup(x => x.GetSessionByJoinCodeAsync(joinCode))
            .ReturnsAsync(session);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/sessions/join/{joinCode}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionDetailsDto>();
        Assert.NotNull(result);
        Assert.Equal(joinCode, result.JoinCode);
    }
}