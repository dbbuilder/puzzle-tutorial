using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.Controllers;

/// <summary>
/// Administrative endpoints requiring admin role
/// </summary>
[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPuzzleRepository _puzzleRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserRepository userRepository,
        IPuzzleRepository puzzleRepository,
        ISessionRepository sessionRepository,
        ILogger<AdminController> logger)
    {
        _userRepository = userRepository;
        _puzzleRepository = puzzleRepository;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get system statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSystemStats()
    {
        try
        {
            var users = await _userRepository.GetActiveUsersAsync(1000);
            var puzzles = await _puzzleRepository.GetPublicPuzzlesAsync(0, 1000);
            var sessions = await _sessionRepository.GetPublicSessionsAsync(0, 1000);

            var stats = new
            {
                totalUsers = users.Count(),
                activeUsers = users.Count(u => u.LastActiveAt > DateTime.UtcNow.AddDays(-7)),
                totalPuzzles = puzzles.Count(),
                activeSessions = sessions.Count(),
                timestamp = DateTime.UtcNow
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system stats");
            return StatusCode(500, new { error = "Failed to retrieve system statistics" });
        }
    }

    /// <summary>
    /// Get all users (paginated)
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var users = await _userRepository.GetActiveUsersAsync(pageSize);
            return Ok(new
            {
                users = users.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.IsActive,
                    u.LastActiveAt,
                    u.CreatedAt
                }),
                page,
                pageSize,
                hasMore = users.Count() == pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }

    /// <summary>
    /// Deactivate a user
    /// </summary>
    [HttpPost("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId)
    {
        try
        {
            var user = await _userRepository.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            user.IsActive = false;
            await _userRepository.UpdateUserAsync(user);

            _logger.LogInformation("User {UserId} deactivated by admin", userId);
            return Ok(new { message = "User deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to deactivate user" });
        }
    }

    /// <summary>
    /// Delete a puzzle (admin only)
    /// </summary>
    [HttpDelete("puzzles/{puzzleId}")]
    public async Task<IActionResult> DeletePuzzle(Guid puzzleId)
    {
        try
        {
            var deleted = await _puzzleRepository.DeletePuzzleAsync(puzzleId);
            if (!deleted)
            {
                return NotFound(new { error = "Puzzle not found" });
            }

            _logger.LogInformation("Puzzle {PuzzleId} deleted by admin", puzzleId);
            return Ok(new { message = "Puzzle deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting puzzle {PuzzleId}", puzzleId);
            return StatusCode(500, new { error = "Failed to delete puzzle" });
        }
    }
}