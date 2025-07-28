using System.Security.Claims;
using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.Controllers;

/// <summary>
/// Controller for managing API keys
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApiKeyController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeyController> _logger;

    public ApiKeyController(IApiKeyService apiKeyService, ILogger<ApiKeyController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new API key for the authenticated user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User not authenticated" });
        }
        
        // Validate requested scopes
        var invalidScopes = request.Scopes.Except(ApiScopes.AllScopes).ToList();
        if (invalidScopes.Any())
        {
            return BadRequest(new { error = $"Invalid scopes: {string.Join(", ", invalidScopes)}" });
        }
        
        // Calculate expiration date
        DateTime? expiresAt = request.ExpiresInDays.HasValue 
            ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value) 
            : null;
        
        try
        {
            var apiKey = await _apiKeyService.GenerateApiKeyAsync(
                userId, 
                request.Name, 
                request.Scopes, 
                expiresAt);
            
            var response = new CreateApiKeyResponse
            {
                Id = apiKey.Id,
                Key = apiKey.Key!, // Only available when creating
                Name = apiKey.Name,
                Scopes = apiKey.Scopes,
                ExpiresAt = apiKey.ExpiresAt,
                CreatedAt = apiKey.CreatedAt
            };
            
            _logger.LogInformation("API key created for user {UserId}: {KeyId}", userId, apiKey.Id);
            
            return CreatedAtAction(nameof(GetApiKeys), new { id = apiKey.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to create API key" });
        }
    }

    /// <summary>
    /// Get all API keys for the authenticated user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiKeyListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApiKeys()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User not authenticated" });
        }
        
        try
        {
            var keys = await _apiKeyService.GetUserApiKeysAsync(userId);
            var response = new ApiKeyListResponse
            {
                Keys = keys.Select(k => new ApiKeyDto
                {
                    Id = k.Id,
                    Name = k.Name,
                    Scopes = k.Scopes,
                    IsActive = k.IsActive,
                    ExpiresAt = k.ExpiresAt,
                    CreatedAt = k.CreatedAt,
                    LastUsedAt = k.LastUsedAt,
                    RevokedAt = k.RevokedAt
                })
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API keys for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve API keys" });
        }
    }

    /// <summary>
    /// Revoke an API key
    /// </summary>
    [HttpDelete("{keyId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(string keyId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "User not authenticated" });
        }
        
        try
        {
            var result = await _apiKeyService.RevokeApiKeyAsync(keyId, userId);
            if (!result)
            {
                return NotFound(new { error = "API key not found or unauthorized" });
            }
            
            _logger.LogInformation("API key {KeyId} revoked by user {UserId}", keyId, userId);
            return Ok(new { success = true, message = "API key revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key {KeyId} for user {UserId}", keyId, userId);
            return StatusCode(500, new { error = "Failed to revoke API key" });
        }
    }

    /// <summary>
    /// Validate an API key (for external services)
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiKeyValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiKeyValidationResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ValidateApiKey([FromBody] ValidateApiKeyRequest request)
    {
        try
        {
            var result = await _apiKeyService.ValidateApiKeyAsync(request.ApiKey);
            
            var response = new ApiKeyValidationResponse
            {
                IsValid = result.IsValid,
                UserId = result.UserId,
                Scopes = result.Scopes,
                Error = result.Error
            };
            
            if (!result.IsValid)
            {
                return Unauthorized(response);
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return StatusCode(500, new ApiKeyValidationResponse 
            { 
                IsValid = false, 
                Error = "Failed to validate API key" 
            });
        }
    }

    /// <summary>
    /// Get available API scopes
    /// </summary>
    [HttpGet("scopes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetAvailableScopes()
    {
        return Ok(new
        {
            scopes = ApiScopes.AllScopes,
            defaultScopes = ApiScopes.DefaultScopes,
            descriptions = new
            {
                read_puzzles = "Read access to puzzles",
                write_puzzles = "Create and update puzzles",
                delete_puzzles = "Delete puzzles",
                read_sessions = "Read access to puzzle sessions",
                write_sessions = "Create and update sessions",
                admin_users = "Administer users",
                admin_system = "System administration"
            }
        });
    }
}