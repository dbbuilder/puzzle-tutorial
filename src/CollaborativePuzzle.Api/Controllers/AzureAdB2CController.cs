using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.Controllers;

/// <summary>
/// Controller for Azure AD B2C authentication endpoints
/// </summary>
[ApiController]
[Route("api/auth/b2c")]
public class AzureAdB2CController : ControllerBase
{
    private readonly IExternalAuthenticationService _authService;
    private readonly ILogger<AzureAdB2CController> _logger;

    public AzureAdB2CController(
        IExternalAuthenticationService authService,
        ILogger<AzureAdB2CController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Login with an Azure AD B2C token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AzureAdB2CLoginRequest request)
    {
        try
        {
            var result = await _authService.ValidateTokenAsync(request.B2CToken);
            
            if (!result.Success)
            {
                _logger.LogWarning("Failed Azure AD B2C login attempt: {Error}", result.Error);
                return Unauthorized(new AzureAdB2CAuthResponse
                {
                    Success = false,
                    Error = result.Error ?? "Authentication failed"
                });
            }
            
            _logger.LogInformation("User {UserId} logged in via Azure AD B2C", result.User!.Id);
            
            return Ok(new AzureAdB2CAuthResponse
            {
                Success = true,
                Token = result.Token,
                User = new UserDto
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Roles = result.Roles
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Azure AD B2C login");
            return StatusCode(500, new AzureAdB2CAuthResponse
            {
                Success = false,
                Error = "An error occurred during Azure AD B2C authentication"
            });
        }
    }

    /// <summary>
    /// Handle Azure AD B2C callback with authorization code
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromBody] AzureAdB2CCallbackRequest request)
    {
        try
        {
            var result = await _authService.ExchangeCodeAsync(request.Code, request.RedirectUri);
            
            if (!result.Success)
            {
                _logger.LogWarning("Failed Azure AD B2C code exchange: {Error}", result.Error);
                return Unauthorized(new AzureAdB2CAuthResponse
                {
                    Success = false,
                    Error = result.Error ?? "Code exchange failed"
                });
            }
            
            _logger.LogInformation("User {UserId} authenticated via Azure AD B2C callback", result.User!.Id);
            
            return Ok(new AzureAdB2CAuthResponse
            {
                Success = true,
                Token = result.Token,
                User = new UserDto
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Roles = result.Roles
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Azure AD B2C callback");
            return StatusCode(500, new AzureAdB2CAuthResponse
            {
                Success = false,
                Error = "An error occurred during Azure AD B2C callback"
            });
        }
    }

    /// <summary>
    /// Get the Azure AD B2C login URL
    /// </summary>
    [HttpGet("login-url")]
    [AllowAnonymous]
    public IActionResult GetLoginUrl([FromQuery] string redirectUri, [FromQuery] string? state = null)
    {
        try
        {
            var loginUrl = _authService.GetLoginUrl(redirectUri, state);
            return Ok(new { loginUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Azure AD B2C login URL");
            return StatusCode(500, new { error = "Failed to generate login URL" });
        }
    }

    /// <summary>
    /// Get the Azure AD B2C logout URL
    /// </summary>
    [HttpGet("logout-url")]
    [AllowAnonymous]
    public IActionResult GetLogoutUrl([FromQuery] string redirectUri)
    {
        try
        {
            var logoutUrl = _authService.GetLogoutUrl(redirectUri);
            return Ok(new { logoutUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Azure AD B2C logout URL");
            return StatusCode(500, new { error = "Failed to generate logout URL" });
        }
    }
}