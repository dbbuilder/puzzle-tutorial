using System.Net;
using System.Net.Http.Headers;
using CollaborativePuzzle.Api.Middleware;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CollaborativePuzzle.Tests.Middleware;

public class JwtAuthenticationMiddlewareTests
{
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly JwtAuthenticationMiddleware _middleware;
    private readonly DefaultHttpContext _context;
    
    public JwtAuthenticationMiddlewareTests()
    {
        _mockJwtService = new Mock<IJwtService>();
        _middleware = new JwtAuthenticationMiddleware(Next);
        _context = new DefaultHttpContext();
        
        // Setup service provider
        var services = new ServiceCollection();
        services.AddSingleton(_mockJwtService.Object);
        _context.RequestServices = services.BuildServiceProvider();
    }
    
    private Task Next(HttpContext context)
    {
        context.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
    
    [Fact]
    public async Task Middleware_WithNoAuthHeader_CallsNext()
    {
        // Act
        await _middleware.InvokeAsync(_context);
        
        // Assert
        _context.Response.StatusCode.Should().Be(200);
        _context.User.Identity!.IsAuthenticated.Should().BeFalse();
    }
    
    [Fact]
    public async Task Middleware_WithValidToken_SetsUser()
    {
        // Arrange
        var token = "valid.jwt.token";
        _context.Request.Headers["Authorization"] = $"Bearer {token}";
        
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        }, "Jwt"));
        
        _mockJwtService.Setup(x => x.ValidateToken(token))
            .Returns(TokenValidationResult.Success(principal));
        
        // Act
        await _middleware.InvokeAsync(_context);
        
        // Assert
        _context.Response.StatusCode.Should().Be(200);
        _context.User.Should().Be(principal);
        _context.User.Identity!.IsAuthenticated.Should().BeTrue();
        _context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user123");
    }
    
    [Fact]
    public async Task Middleware_WithInvalidToken_DoesNotSetUser()
    {
        // Arrange
        var token = "invalid.jwt.token";
        _context.Request.Headers["Authorization"] = $"Bearer {token}";
        
        _mockJwtService.Setup(x => x.ValidateToken(token))
            .Returns(TokenValidationResult.Failure("Invalid token"));
        
        // Act
        await _middleware.InvokeAsync(_context);
        
        // Assert
        _context.Response.StatusCode.Should().Be(200);
        _context.User.Identity!.IsAuthenticated.Should().BeFalse();
    }
    
    [Fact]
    public async Task Middleware_WithMalformedAuthHeader_DoesNotSetUser()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "InvalidFormat token";
        
        // Act
        await _middleware.InvokeAsync(_context);
        
        // Assert
        _context.Response.StatusCode.Should().Be(200);
        _context.User.Identity!.IsAuthenticated.Should().BeFalse();
        _mockJwtService.Verify(x => x.ValidateToken(It.IsAny<string>()), Times.Never);
    }
    
    [Theory]
    [InlineData("bearer token123")] // lowercase
    [InlineData("Bearer token123")] // uppercase
    [InlineData("BEARER token123")] // all caps
    public async Task Middleware_WithDifferentBearerCasing_ExtractsToken(string authHeader)
    {
        // Arrange
        _context.Request.Headers["Authorization"] = authHeader;
        
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123")
        }, "Jwt"));
        
        _mockJwtService.Setup(x => x.ValidateToken("token123"))
            .Returns(TokenValidationResult.Success(principal));
        
        // Act
        await _middleware.InvokeAsync(_context);
        
        // Assert
        _context.User.Identity!.IsAuthenticated.Should().BeTrue();
        _mockJwtService.Verify(x => x.ValidateToken("token123"), Times.Once);
    }
}