using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TokenValidationResult = CollaborativePuzzle.Core.Interfaces.TokenValidationResult;

namespace CollaborativePuzzle.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _key = Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("JWT secret key not configured"));
        _issuer = configuration["Jwt:Issuer"] ?? "CollaborativePuzzle";
        _audience = configuration["Jwt:Audience"] ?? "CollaborativePuzzleUsers";
        _expirationMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "60");
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("jti", Guid.NewGuid().ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add roles
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Additional validation
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return TokenValidationResult.Failure("Invalid token algorithm");
            }

            return TokenValidationResult.Success(principal);
        }
        catch (SecurityTokenExpiredException)
        {
            return TokenValidationResult.Failure("Token has expired");
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            return TokenValidationResult.Failure("Invalid token issuer");
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            return TokenValidationResult.Failure("Invalid token audience");
        }
        catch (Exception ex)
        {
            return TokenValidationResult.Failure($"Token validation failed: {ex.Message}");
        }
    }

    public RefreshTokenResult RefreshToken(string token)
    {
        var validationResult = ValidateToken(token);
        
        // Allow refresh even if expired, but must be otherwise valid
        if (!validationResult.IsValid && !validationResult.Error!.Contains("expired"))
        {
            return RefreshTokenResult.Failure(validationResult.Error!);
        }

        try
        {
            // Parse token without validation to get claims
            var jwt = _tokenHandler.ReadJwtToken(token);
            var userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var username = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
            {
                return RefreshTokenResult.Failure("Invalid token claims");
            }

            var user = new User { Id = userId, Username = username, Email = email };
            var newToken = GenerateToken(user, roles);

            return RefreshTokenResult.Success(newToken);
        }
        catch (Exception ex)
        {
            return RefreshTokenResult.Failure($"Failed to refresh token: {ex.Message}");
        }
    }

    public string? GetUserIdFromToken(string token)
    {
        try
        {
            var jwt = _tokenHandler.ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public IEnumerable<string> GetRolesFromToken(string token)
    {
        try
        {
            var jwt = _tokenHandler.ReadJwtToken(token);
            return jwt.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }
}