using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests.Services;

public class JwtServiceTests
{
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly string _testSecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly12345678";
    
    public JwtServiceTests()
    {
        var configData = new Dictionary<string, string>
        {
            {"Jwt:SecretKey", _testSecretKey},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"},
            {"Jwt:ExpirationInMinutes", "60"}
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
            
        _jwtService = new JwtService(_configuration);
    }
    
    [Fact]
    public void GenerateToken_WithValidUser_ReturnsValidJwt()
    {
        // Arrange
        var user = new User
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var roles = new[] { "User", "Player" };
        
        // Act
        var token = _jwtService.GenerateToken(user, roles);
        
        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Validate token structure
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.CanReadToken(token).Should().BeTrue();
        
        var jwt = tokenHandler.ReadJwtToken(token);
        jwt.Should().NotBeNull();
        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }
    
    [Fact]
    public void GenerateToken_IncludesRequiredClaims()
    {
        // Arrange
        var user = new User
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var roles = new[] { "Admin" };
        
        // Act
        var token = _jwtService.GenerateToken(user, roles);
        
        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(token);
        
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user123");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jwt.Claims.Should().Contain(c => c.Type == "jti"); // JWT ID
    }
    
    [Fact]
    public void ValidateToken_WithValidToken_ReturnsClaimsPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var token = _jwtService.GenerateToken(user, new[] { "User" });
        
        // Act
        var result = _jwtService.ValidateToken(token);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user123");
    }
    
    [Fact]
    public void ValidateToken_WithExpiredToken_ReturnsInvalid()
    {
        // Arrange
        var expiredConfig = new Dictionary<string, string>
        {
            {"Jwt:SecretKey", _testSecretKey},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"},
            {"Jwt:ExpirationInMinutes", "-1"} // Expired
        };
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(expiredConfig!)
            .Build();
            
        var service = new JwtService(config);
        var user = new User { Id = "user123", Username = "test", Email = "test@example.com" };
        var token = service.GenerateToken(user, new[] { "User" });
        
        // Act
        var result = _jwtService.ValidateToken(token);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }
    
    [Fact]
    public void ValidateToken_WithInvalidSignature_ReturnsInvalid()
    {
        // Arrange
        var user = new User { Id = "user123", Username = "test", Email = "test@example.com" };
        var token = _jwtService.GenerateToken(user, new[] { "User" });
        
        // Tamper with token
        var parts = token.Split('.');
        parts[2] = "tamperedsignature";
        var tamperedToken = string.Join(".", parts);
        
        // Act
        var result = _jwtService.ValidateToken(tamperedToken);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void ValidateToken_WithWrongIssuer_ReturnsInvalid()
    {
        // Arrange - Create token with different issuer
        var wrongIssuerConfig = new Dictionary<string, string>
        {
            {"Jwt:SecretKey", _testSecretKey},
            {"Jwt:Issuer", "WrongIssuer"},
            {"Jwt:Audience", "TestAudience"},
            {"Jwt:ExpirationInMinutes", "60"}
        };
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(wrongIssuerConfig!)
            .Build();
            
        var wrongService = new JwtService(config);
        var user = new User { Id = "user123", Username = "test", Email = "test@example.com" };
        var token = wrongService.GenerateToken(user, new[] { "User" });
        
        // Act - Validate with original service (different issuer)
        var result = _jwtService.ValidateToken(token);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("issuer");
    }
    
    [Fact]
    public void RefreshToken_WithValidToken_ReturnsNewToken()
    {
        // Arrange
        var user = new User
        {
            Id = "user123",
            Username = "testuser",
            Email = "test@example.com"
        };
        var originalToken = _jwtService.GenerateToken(user, new[] { "User" });
        
        // Act
        var refreshResult = _jwtService.RefreshToken(originalToken);
        
        // Assert
        refreshResult.Should().NotBeNull();
        refreshResult.IsSuccess.Should().BeTrue();
        refreshResult.Token.Should().NotBeNullOrEmpty();
        refreshResult.Token.Should().NotBe(originalToken); // New token
        
        // Verify new token is valid
        var validationResult = _jwtService.ValidateToken(refreshResult.Token!);
        validationResult.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void GetUserIdFromToken_ReturnsCorrectUserId()
    {
        // Arrange
        var user = new User { Id = "user123", Username = "test", Email = "test@example.com" };
        var token = _jwtService.GenerateToken(user, new[] { "User" });
        
        // Act
        var userId = _jwtService.GetUserIdFromToken(token);
        
        // Assert
        userId.Should().Be("user123");
    }
    
    [Fact]
    public void GetRolesFromToken_ReturnsCorrectRoles()
    {
        // Arrange
        var user = new User { Id = "user123", Username = "test", Email = "test@example.com" };
        var roles = new[] { "User", "Admin", "Moderator" };
        var token = _jwtService.GenerateToken(user, roles);
        
        // Act
        var extractedRoles = _jwtService.GetRolesFromToken(token);
        
        // Assert
        extractedRoles.Should().BeEquivalentTo(roles);
    }
}