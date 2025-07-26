using CollaborativePuzzle.Api.Controllers;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CollaborativePuzzle.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPuzzleRepository> _puzzleRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<ILogger<AdminController>> _loggerMock;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _puzzleRepositoryMock = new Mock<IPuzzleRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _loggerMock = new Mock<ILogger<AdminController>>();
        
        _controller = new AdminController(
            _userRepositoryMock.Object,
            _puzzleRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _loggerMock.Object);

        // Set up admin user context
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin123"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "Test"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };
    }

    [Fact]
    public async Task GetSystemStats_ReturnsOkWithStats()
    {
        // Arrange
        var users = new List<Core.Entities.User>
        {
            new() { Id = Guid.NewGuid(), Username = "user1", LastActiveAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "user2", LastActiveAt = DateTime.UtcNow.AddDays(-10) }
        };

        var puzzles = new List<Puzzle>
        {
            new() { Id = Guid.NewGuid(), Name = "Puzzle1" },
            new() { Id = Guid.NewGuid(), Name = "Puzzle2" }
        };

        var sessions = new List<PuzzleSession>
        {
            new() { Id = Guid.NewGuid(), Status = Core.Enums.SessionStatus.Active }
        };

        _userRepositoryMock.Setup(x => x.GetActiveUsersAsync(1000))
            .ReturnsAsync(users);
        _puzzleRepositoryMock.Setup(x => x.GetPuzzlesAsync(1, 1000))
            .ReturnsAsync(puzzles);
        _sessionRepositoryMock.Setup(x => x.GetActiveSessionsAsync(1000))
            .ReturnsAsync(sessions);

        // Act
        var result = await _controller.GetSystemStats();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        
        dynamic stats = okResult.Value!;
        ((int)stats.totalUsers).Should().Be(2);
        ((int)stats.activeUsers).Should().Be(1);
        ((int)stats.totalPuzzles).Should().Be(2);
        ((int)stats.activeSessions).Should().Be(1);
    }

    [Fact]
    public async Task GetUsers_ReturnsOkWithUsersList()
    {
        // Arrange
        var users = new List<Core.Entities.User>
        {
            new() 
            { 
                Id = Guid.NewGuid(), 
                Username = "user1", 
                Email = "user1@test.com",
                IsActive = true,
                LastActiveAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        };

        _userRepositoryMock.Setup(x => x.GetActiveUsersAsync(20))
            .ReturnsAsync(users);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateUser_WithValidUser_ReturnsOkAndDeactivatesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new Core.Entities.User
        {
            Id = userId,
            Username = "testuser",
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateUserAsync(It.IsAny<Core.Entities.User>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeactivateUser(userId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        user.IsActive.Should().BeFalse();
        _userRepositoryMock.Verify(x => x.UpdateUserAsync(It.Is<Core.Entities.User>(u => !u.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_WithInvalidUser_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepositoryMock.Setup(x => x.GetUserAsync(userId))
            .ReturnsAsync((Core.Entities.User?)null);

        // Act
        var result = await _controller.DeactivateUser(userId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeletePuzzle_WithValidPuzzle_ReturnsOkAndDeletesPuzzle()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        _puzzleRepositoryMock.Setup(x => x.DeletePuzzleAsync(puzzleId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeletePuzzle(puzzleId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _puzzleRepositoryMock.Verify(x => x.DeletePuzzleAsync(puzzleId), Times.Once);
    }

    [Fact]
    public async Task DeletePuzzle_WithInvalidPuzzle_ReturnsNotFound()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        _puzzleRepositoryMock.Setup(x => x.DeletePuzzleAsync(puzzleId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeletePuzzle(puzzleId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void AdminController_ShouldHaveRequireAdminAttribute()
    {
        // Arrange & Act
        var attributes = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);

        // Assert
        attributes.Should().HaveCount(1);
    }
}