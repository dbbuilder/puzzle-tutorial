using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CollaborativePuzzle.Api.Authentication;

/// <summary>
/// Service for handling Azure AD B2C authentication
/// </summary>
public class AzureAdB2CAuthenticationService : IExternalAuthenticationService
{
    private readonly AzureAdB2CSettings _settings;
    private readonly IUserService _userService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AzureAdB2CAuthenticationService> _logger;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private OpenIdConnectConfiguration? _openIdConfig;
    private DateTime _lastConfigRefresh = DateTime.MinValue;
    private readonly TimeSpan _configRefreshInterval = TimeSpan.FromHours(1);

    public AzureAdB2CAuthenticationService(
        IOptions<AzureAdB2CSettings> settings,
        IUserService userService,
        IJwtService jwtService,
        ILogger<AzureAdB2CAuthenticationService> logger)
    {
        _settings = settings.Value;
        _userService = userService;
        _jwtService = jwtService;
        _logger = logger;
        
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _settings.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    /// <summary>
    /// Validates an Azure AD B2C token and returns user information
    /// </summary>
    public async Task<ExternalAuthenticationResult> ValidateTokenAsync(string token)
    {
        try
        {
            // Get OpenID configuration
            var config = await GetOpenIdConfigurationAsync();
            
            // Set up token validation parameters
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = _settings.ValidateIssuer,
                ValidIssuers = new[] { _settings.Authority, $"{_settings.Instance}/{_settings.TenantId}/v2.0" },
                ValidateAudience = true,
                ValidAudiences = _settings.ValidAudiences,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Validate the token
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Extract user information from claims
            var userId = principal.FindFirst("oid")?.Value ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("emails")?.Value ?? principal.FindFirst("email")?.Value;
            var name = principal.FindFirst("name")?.Value ?? principal.FindFirst("given_name")?.Value;
            
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                return ExternalAuthenticationResult.CreateFailure("Invalid token: missing required claims");
            }

            // Check if user exists in our system
            var existingUser = await _userService.GetUserByIdAsync(userId);
            if (existingUser == null)
            {
                // Create new user from Azure AD B2C
                var registerRequest = new RegisterRequest
                {
                    Username = email.Split('@')[0], // Use email prefix as username
                    Email = email,
                    Password = GenerateRandomPassword(), // Generate a random password for B2C users
                    ConfirmPassword = GenerateRandomPassword()
                };
                
                var createResult = await _userService.CreateUserAsync(registerRequest);
                if (!createResult.Success)
                {
                    return ExternalAuthenticationResult.CreateFailure($"Failed to create user: {createResult.Error}");
                }
                
                existingUser = createResult.User;
            }

            // Get user roles
            var roles = await _userService.GetUserRolesAsync(userId);
            
            // Generate our internal JWT token
            var internalToken = _jwtService.GenerateToken(existingUser!, roles);
            
            return ExternalAuthenticationResult.CreateSuccess(existingUser!, internalToken, roles);
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return ExternalAuthenticationResult.CreateFailure($"Token validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Azure AD B2C token");
            return ExternalAuthenticationResult.CreateFailure("An error occurred during authentication");
        }
    }

    /// <summary>
    /// Exchanges an authorization code for tokens
    /// </summary>
    public async Task<ExternalAuthenticationResult> ExchangeCodeAsync(string code, string redirectUri)
    {
        try
        {
            // In a real implementation, you would exchange the code for tokens using MSAL
            // For now, this is a placeholder
            throw new NotImplementedException("Code exchange not implemented. Use MSAL library for production.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code");
            return ExternalAuthenticationResult.CreateFailure("Failed to exchange authorization code");
        }
    }

    /// <summary>
    /// Gets the Azure AD B2C login URL
    /// </summary>
    public string GetLoginUrl(string redirectUri, string? state = null)
    {
        var stateParam = string.IsNullOrEmpty(state) ? Guid.NewGuid().ToString() : state;
        
        return $"{_settings.Authority}/oauth2/v2.0/authorize?" +
               $"client_id={_settings.ClientId}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_mode=query" +
               $"&scope=openid%20email%20profile" +
               $"&state={stateParam}";
    }

    /// <summary>
    /// Gets the Azure AD B2C logout URL
    /// </summary>
    public string GetLogoutUrl(string redirectUri)
    {
        return $"{_settings.Authority}/oauth2/v2.0/logout?" +
               $"post_logout_redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    private async Task<OpenIdConnectConfiguration> GetOpenIdConfigurationAsync()
    {
        // Cache the configuration to avoid frequent requests
        if (_openIdConfig == null || DateTime.UtcNow - _lastConfigRefresh > _configRefreshInterval)
        {
            _openIdConfig = await _configurationManager.GetConfigurationAsync(CancellationToken.None);
            _lastConfigRefresh = DateTime.UtcNow;
        }
        
        return _openIdConfig;
    }

    private string GenerateRandomPassword()
    {
        // Generate a cryptographically secure random password for B2C users
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var result = new char[16];
        
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            var bytes = new byte[16];
            rng.GetBytes(bytes);
            
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }
        }
        
        return new string(result);
    }
}

