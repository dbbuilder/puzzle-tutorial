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

public class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IExternalAuthenticationService> _mockExternalAuthService;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _mockUserService = new Mock<IUserService>();
        _mockJwtService = new Mock<IJwtService>();
        _mockExternalAuthService = new Mock<IExternalAuthenticationService>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing services
                var userServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserService));
                if (userServiceDescriptor != null) services.Remove(userServiceDescriptor);
                
                var jwtServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IJwtService));
                if (jwtServiceDescriptor != null) services.Remove(jwtServiceDescriptor);
                
                var externalAuthDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IExternalAuthenticationService));
                if (externalAuthDescriptor != null) services.Remove(externalAuthDescriptor);
                
                // Add mocks
                services.AddSingleton(_mockUserService.Object);
                services.AddSingleton(_mockJwtService.Object);
                services.AddSingleton(_mockExternalAuthService.Object);
            });
        });
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "password123"
        };
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = "test@example.com"
        };
        
        var roles = new[] { "User" };
        
        _mockUserService.Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(new AuthenticationResult { Success = true, User = user, Roles = roles });
        
        _mockJwtService.Setup(x => x.GenerateToken(user, roles))
            .Returns("test-jwt-token");
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("test-jwt-token", result.Token);
        Assert.NotNull(result.User);
        Assert.Equal(user.Username, result.User.Username);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "wrongpassword"
        };
        
        _mockUserService.Setup(x => x.ValidateCredentialsAsync(request.Username, request.Password))
            .ReturnsAsync(new AuthenticationResult { Success = false });
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email
        };
        
        _mockUserService.Setup(x => x.CreateUserAsync(request))
            .ReturnsAsync(new CreateUserResult { Success = true, User = user });
        
        _mockJwtService.Setup(x => x.GenerateToken(user, It.IsAny<string[]>()))
            .Returns("test-jwt-token");
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("test-jwt-token", result.Token);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "password123",
            ConfirmPassword = "differentpassword"
        };
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            Token = "valid-refresh-token"
        };
        
        _mockJwtService.Setup(x => x.RefreshToken(request.Token))
            .Returns(new TokenRefreshResult { IsSuccess = true, Token = "new-jwt-token" });
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("new-jwt-token", result.Token);
    }

    [Fact]
    public async Task B2CLogin_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new AzureAdB2CLoginRequest
        {
            B2CToken = "valid-b2c-token"
        };
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "b2cuser",
            Email = "b2c@example.com"
        };
        
        var result = ExternalAuthenticationResult.CreateSuccess(user, "jwt-token", new[] { "User" });
        
        _mockExternalAuthService.Setup(x => x.ValidateTokenAsync(request.B2CToken))
            .ReturnsAsync(result);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/b2c/login", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<AzureAdB2CAuthResponse>();
        Assert.NotNull(authResponse);
        Assert.True(authResponse.Success);
        Assert.Equal("jwt-token", authResponse.Token);
    }

    [Fact]
    public async Task GetB2CLoginUrl_ReturnsOk()
    {
        // Arrange
        var redirectUri = "https://example.com/callback";
        var loginUrl = "https://b2c.example.com/authorize?redirect_uri=https://example.com/callback";
        
        _mockExternalAuthService.Setup(x => x.GetLoginUrl(redirectUri, null))
            .Returns(loginUrl);
        
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync($"/api/v1/auth/b2c/login-url?redirectUri={Uri.EscapeDataString(redirectUri)}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(loginUrl, content);
    }
}