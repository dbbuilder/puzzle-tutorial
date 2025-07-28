using CollaborativePuzzle.Api.Controllers;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Controllers;

public class AzureAdB2CControllerTests
{
    private readonly Mock<IExternalAuthenticationService> _mockAuthService;
    private readonly Mock<ILogger<AzureAdB2CController>> _mockLogger;
    private readonly AzureAdB2CController _controller;

    public AzureAdB2CControllerTests()
    {
        _mockAuthService = new Mock<IExternalAuthenticationService>();
        _mockLogger = new Mock<ILogger<AzureAdB2CController>>();
        _controller = new AzureAdB2CController(_mockAuthService.Object, _mockLogger.Object);
        
        // Set up HttpContext for the controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Login_WithValidB2CToken_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AzureAdB2CLoginRequest { B2CToken = "valid-b2c-token" };
        var user = new User { Id = "user-id", Username = "testuser", Email = "test@example.com" };
        var roles = new[] { "User" };
        var internalToken = "internal-jwt-token";
        var authResult = ExternalAuthenticationResult.CreateSuccess(user, internalToken, roles);
        
        _mockAuthService.Setup(x => x.ValidateTokenAsync(request.B2CToken))
            .ReturnsAsync(authResult);
        
        // Act
        var result = await _controller.Login(request);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        Assert.True(response.success);
        Assert.Equal(internalToken, response.token);
        Assert.NotNull(response.user);
    }

    [Fact]
    public async Task Login_WithInvalidB2CToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AzureAdB2CLoginRequest { B2CToken = "invalid-b2c-token" };
        var authResult = ExternalAuthenticationResult.CreateFailure("Invalid token");
        
        _mockAuthService.Setup(x => x.ValidateTokenAsync(request.B2CToken))
            .ReturnsAsync(authResult);
        
        // Act
        var result = await _controller.Login(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        dynamic response = unauthorizedResult.Value!;
        Assert.False(response.success);
        Assert.Equal("Invalid token", response.error);
    }

    [Fact]
    public async Task Login_WithException_Returns500()
    {
        // Arrange
        var request = new AzureAdB2CLoginRequest { B2CToken = "token" };
        
        _mockAuthService.Setup(x => x.ValidateTokenAsync(request.B2CToken))
            .ThrowsAsync(new Exception("Test exception"));
        
        // Act
        var result = await _controller.Login(request);
        
        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        dynamic response = statusResult.Value!;
        Assert.False(response.success);
        Assert.Equal("An error occurred during Azure AD B2C authentication", response.error);
    }

    [Fact]
    public async Task Callback_WithValidCode_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AzureAdB2CCallbackRequest
        {
            Code = "auth-code",
            State = "state",
            RedirectUri = "https://localhost/callback"
        };
        var user = new User { Id = "user-id", Username = "testuser", Email = "test@example.com" };
        var roles = new[] { "User" };
        var internalToken = "internal-jwt-token";
        var authResult = ExternalAuthenticationResult.CreateSuccess(user, internalToken, roles);
        
        _mockAuthService.Setup(x => x.ExchangeCodeAsync(request.Code, request.RedirectUri))
            .ReturnsAsync(authResult);
        
        // Act
        var result = await _controller.Callback(request);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        Assert.True(response.success);
        Assert.Equal(internalToken, response.token);
        Assert.NotNull(response.user);
    }

    [Fact]
    public async Task Callback_WithInvalidCode_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AzureAdB2CCallbackRequest
        {
            Code = "invalid-code",
            State = "state",
            RedirectUri = "https://localhost/callback"
        };
        var authResult = ExternalAuthenticationResult.CreateFailure("Invalid authorization code");
        
        _mockAuthService.Setup(x => x.ExchangeCodeAsync(request.Code, request.RedirectUri))
            .ReturnsAsync(authResult);
        
        // Act
        var result = await _controller.Callback(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        dynamic response = unauthorizedResult.Value!;
        Assert.False(response.success);
        Assert.Equal("Invalid authorization code", response.error);
    }

    [Fact]
    public void GetLoginUrl_ReturnsOkWithUrl()
    {
        // Arrange
        var redirectUri = "https://localhost/callback";
        var state = "test-state";
        var expectedUrl = "https://testb2c.b2clogin.com/authorize?...";
        
        _mockAuthService.Setup(x => x.GetLoginUrl(redirectUri, state))
            .Returns(expectedUrl);
        
        // Act
        var result = _controller.GetLoginUrl(redirectUri, state);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        Assert.Equal(expectedUrl, response.loginUrl);
    }

    [Fact]
    public void GetLogoutUrl_ReturnsOkWithUrl()
    {
        // Arrange
        var redirectUri = "https://localhost/logout-callback";
        var expectedUrl = "https://testb2c.b2clogin.com/logout?...";
        
        _mockAuthService.Setup(x => x.GetLogoutUrl(redirectUri))
            .Returns(expectedUrl);
        
        // Act
        var result = _controller.GetLogoutUrl(redirectUri);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        Assert.Equal(expectedUrl, response.logoutUrl);
    }
}