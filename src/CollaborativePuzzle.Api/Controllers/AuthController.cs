using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, IJwtService jwtService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var authResult = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            
            if (!authResult.Success)
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Error = authResult.Error ?? "Invalid credentials"
                });
            }

            var token = _jwtService.GenerateToken(authResult.User!, authResult.Roles);
            
            _logger.LogInformation("User {Username} logged in successfully", request.Username);
            
            return Ok(new LoginResponse
            {
                Success = true,
                Token = token,
                User = new UserDto
                {
                    Id = authResult.User!.Id,
                    Username = authResult.User.Username,
                    Email = authResult.User.Email,
                    Roles = authResult.Roles
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return StatusCode(500, new LoginResponse
            {
                Success = false,
                Error = "An error occurred during login"
            });
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var createResult = await _userService.CreateUserAsync(request);
            
            if (!createResult.Success)
            {
                _logger.LogWarning("Failed registration attempt: {Error}", createResult.Error);
                return BadRequest(new RegisterResponse
                {
                    Success = false,
                    Error = createResult.Error ?? "Registration failed"
                });
            }

            // Generate token for the new user with default role
            var roles = new[] { "User" };
            var token = _jwtService.GenerateToken(createResult.User!, roles);
            
            _logger.LogInformation("New user registered: {Username}", createResult.User!.Username);
            
            return Ok(new RegisterResponse
            {
                Success = true,
                Token = token,
                User = new UserDto
                {
                    Id = createResult.User!.Id,
                    Username = createResult.User.Username,
                    Email = createResult.User.Email,
                    Roles = roles
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new RegisterResponse
            {
                Success = false,
                Error = "An error occurred during registration"
            });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var refreshResult = _jwtService.RefreshToken(request.Token);
            
            if (!refreshResult.IsSuccess)
            {
                _logger.LogWarning("Failed token refresh attempt");
                return Unauthorized(new RefreshTokenResponse
                {
                    Success = false,
                    Error = refreshResult.Error ?? "Failed to refresh token"
                });
            }
            
            _logger.LogInformation("Token refreshed successfully");
            
            return Ok(new RefreshTokenResponse
            {
                Success = true,
                Token = refreshResult.Token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new RefreshTokenResponse
            {
                Success = false,
                Error = "An error occurred during token refresh"
            });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        // In a real application, you might want to:
        // - Invalidate the token (if using a token blacklist)
        // - Clear any server-side session
        // - Log the logout event
        
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        _logger.LogInformation("User {UserId} logged out", userId);
        
        return Ok(new { message = "Logged out successfully" });
    }
}