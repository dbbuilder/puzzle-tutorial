using CollaborativePuzzle.Api.Controllers;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockJwtService = new Mock<IJwtService>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_mockUserService.Object, _mockJwtService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenResponse()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "Test123!"
        };

        var user = new User
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };

        var roles = new[] { "User", "Player" };
        var token = "valid.jwt.token";

        _mockUserService.Setup(x => x.ValidateCredentialsAsync(loginRequest.Username, loginRequest.Password))
            .ReturnsAsync(new AuthenticationResult { Success = true, User = user, Roles = roles });

        _mockJwtService.Setup(x => x.GenerateToken(user, roles))
            .Returns(token);

        // Act
        var result = await _controller.Login(loginRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.Success.Should().BeTrue();
        response.Token.Should().Be(token);
        response.User.Should().NotBeNull();
        response.User!.Id.Should().Be("user123");
        response.User.Username.Should().Be("testuser");
        response.User.Roles.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "wrongpassword"
        };

        _mockUserService.Setup(x => x.ValidateCredentialsAsync(loginRequest.Username, loginRequest.Password))
            .ReturnsAsync(new AuthenticationResult { Success = false, Error = "Invalid credentials" });

        // Act
        var result = await _controller.Login(loginRequest);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<LoginResponse>().Subject;
        
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Invalid credentials");
        response.Token.Should().BeNull();
        response.User.Should().BeNull();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccessResponse()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        var createdUser = new User
        {
            Id = "newuser123",
            Username = "newuser",
            Email = "newuser@example.com"
        };

        _mockUserService.Setup(x => x.CreateUserAsync(registerRequest))
            .ReturnsAsync(new CreateUserResult { Success = true, User = createdUser });

        var token = "new.jwt.token";
        _mockJwtService.Setup(x => x.GenerateToken(createdUser, It.IsAny<IEnumerable<string>>()))
            .Returns(token);

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RegisterResponse>().Subject;
        
        response.Success.Should().BeTrue();
        response.Token.Should().Be(token);
        response.User.Should().NotBeNull();
        response.User!.Id.Should().Be("newuser123");
    }

    [Fact]
    public async Task Register_WithExistingUsername_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Username = "existinguser",
            Email = "new@example.com",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        _mockUserService.Setup(x => x.CreateUserAsync(registerRequest))
            .ReturnsAsync(new CreateUserResult { Success = false, Error = "Username already exists" });

        // Act
        var result = await _controller.Register(registerRequest);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<RegisterResponse>().Subject;
        
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Username already exists");
        response.Token.Should().BeNull();
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewToken()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest { Token = "old.jwt.token" };
        var newToken = "new.jwt.token";

        _mockJwtService.Setup(x => x.RefreshToken(refreshRequest.Token))
            .Returns(RefreshTokenResult.Success(newToken));

        // Act
        var result = await _controller.RefreshToken(refreshRequest);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RefreshTokenResponse>().Subject;
        
        response.Success.Should().BeTrue();
        response.Token.Should().Be(newToken);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest { Token = "invalid.jwt.token" };

        _mockJwtService.Setup(x => x.RefreshToken(refreshRequest.Token))
            .Returns(RefreshTokenResult.Failure("Token is invalid"));

        // Act
        var result = await _controller.RefreshToken(refreshRequest);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var response = unauthorizedResult.Value.Should().BeOfType<RefreshTokenResponse>().Subject;
        
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Token is invalid");
    }

    [Fact]
    public async Task Logout_ReturnsOkResult()
    {
        // Act
        var result = await _controller.Logout();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var response = (result as OkObjectResult)!.Value;
        response.Should().BeEquivalentTo(new { message = "Logged out successfully" });
    }
}