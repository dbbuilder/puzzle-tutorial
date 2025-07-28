using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CollaborativePuzzle.Api.Controllers;

/// <summary>
/// Controller for puzzle-related operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PuzzleController : ControllerBase
{
    private readonly IPuzzleRepository _puzzleRepository;
    private readonly IPieceRepository _pieceRepository;
    private readonly IPuzzleGeneratorService _puzzleGeneratorService;
    private readonly ILogger<PuzzleController> _logger;

    public PuzzleController(
        IPuzzleRepository puzzleRepository,
        IPieceRepository pieceRepository,
        IPuzzleGeneratorService puzzleGeneratorService,
        ILogger<PuzzleController> logger)
    {
        _puzzleRepository = puzzleRepository;
        _pieceRepository = pieceRepository;
        _puzzleGeneratorService = puzzleGeneratorService;
        _logger = logger;
    }

    /// <summary>
    /// Get all public puzzles (no authentication required)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPuzzles([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var puzzles = await _puzzleRepository.GetPublicPuzzlesAsync((page - 1) * pageSize, pageSize);
            return Ok(new
            {
                puzzles = puzzles.Select(p => new PuzzleDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    PieceCount = p.PieceCount,
                    Difficulty = p.Difficulty.ToString(),
                    Category = p.Category,
                    CreatedAt = p.CreatedAt,
                    AverageRating = p.AverageRating,
                    TotalSessions = p.TotalSessions,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUsername = p.CreatedByUser?.Username ?? "Unknown",
                    PiecesDataUrl = p.PiecesDataUrl,
                    Width = p.Width,
                    Height = p.Height,
                    GridColumns = p.GridColumns,
                    GridRows = p.GridRows,
                    EstimatedCompletionMinutes = p.EstimatedCompletionMinutes,
                    Tags = p.Tags,
                    IsPublic = p.IsPublic,
                    IsFeatured = p.IsFeatured,
                    TotalCompletions = p.TotalCompletions,
                    TotalRatings = p.TotalRatings
                }),
                page,
                pageSize,
                hasMore = puzzles.Count() == pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting puzzles");
            return StatusCode(500, new { error = "Failed to retrieve puzzles" });
        }
    }

    /// <summary>
    /// Get a specific puzzle (no authentication required)
    /// </summary>
    [HttpGet("{puzzleId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPuzzle(Guid puzzleId)
    {
        try
        {
            var puzzle = await _puzzleRepository.GetPuzzleWithPiecesAsync(puzzleId);
            if (puzzle == null)
            {
                return NotFound(new { error = "Puzzle not found" });
            }

            return Ok(new 
            {
                puzzle = new PuzzleDto
                {
                    Id = puzzle.Id,
                    Title = puzzle.Title,
                    Description = puzzle.Description,
                    ImageUrl = puzzle.ImageUrl,
                    PieceCount = puzzle.PieceCount,
                    Difficulty = puzzle.Difficulty.ToString(),
                    Category = puzzle.Category,
                    CreatedAt = puzzle.CreatedAt,
                    AverageRating = puzzle.AverageRating,
                    TotalSessions = puzzle.TotalSessions,
                    CreatedByUserId = puzzle.CreatedByUserId,
                    CreatedByUsername = puzzle.CreatedByUser?.Username ?? "Unknown",
                    PiecesDataUrl = puzzle.PiecesDataUrl,
                    Width = puzzle.Width,
                    Height = puzzle.Height,
                    GridColumns = puzzle.GridColumns,
                    GridRows = puzzle.GridRows,
                    EstimatedCompletionMinutes = puzzle.EstimatedCompletionMinutes,
                    Tags = puzzle.Tags,
                    IsPublic = puzzle.IsPublic,
                    IsFeatured = puzzle.IsFeatured,
                    TotalCompletions = puzzle.TotalCompletions,
                    TotalRatings = puzzle.TotalRatings
                },
                pieces = puzzle.Pieces?.Select(p => new PuzzlePieceDto
                {
                    Id = p.Id,
                    PuzzleId = p.PuzzleId,
                    PieceNumber = p.PieceNumber,
                    GridX = p.GridX,
                    GridY = p.GridY,
                    CorrectX = p.CorrectX,
                    CorrectY = p.CorrectY,
                    CurrentX = p.CurrentX,
                    CurrentY = p.CurrentY,
                    Rotation = p.Rotation,
                    ShapeData = p.ShapeData,
                    ImageX = p.ImageX,
                    ImageY = p.ImageY,
                    ImageWidth = p.ImageWidth,
                    ImageHeight = p.ImageHeight,
                    IsPlaced = p.IsPlaced,
                    IsEdgePiece = p.IsEdgePiece,
                    IsCornerPiece = p.IsCornerPiece,
                    LockedByUserId = p.LockedByUserId,
                    LockedByUsername = p.LockedByUser?.Username,
                    LockedAt = p.LockedAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting puzzle {PuzzleId}", puzzleId);
            return StatusCode(500, new { error = "Failed to retrieve puzzle" });
        }
    }

    /// <summary>
    /// Create a new puzzle (requires User role)
    /// </summary>
    [HttpPost]
    [RequireUser]
    public async Task<IActionResult> CreatePuzzle([FromBody] CreatePuzzleRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var puzzle = new Puzzle
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                PieceCount = request.PieceCount,
                Difficulty = Enum.TryParse<PuzzleDifficulty>(request.Category, out var diff) ? diff : PuzzleDifficulty.Medium,
                CreatedByUserId = Guid.Parse(userId),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = request.IsPublic,
                Category = request.Category,
                Tags = request.Tags,
                // Default values for now - should be calculated based on image
                Width = 1000,
                Height = 800,
                GridColumns = 10,
                GridRows = 8,
                PiecesDataUrl = request.ImageUrl, // Will be updated after processing
                EstimatedCompletionMinutes = request.PieceCount / 10 // Rough estimate
            };

            // Generate puzzle pieces based on image and piece count
            var pieces = await _puzzleGeneratorService.GeneratePuzzlePiecesAsync(
                puzzle.ImageUrl,
                puzzle.PieceCount,
                puzzle.Id,
                puzzle.Width,
                puzzle.Height
            );
            
            await _puzzleRepository.CreatePuzzleAsync(puzzle, pieces);

            _logger.LogInformation("Puzzle {PuzzleId} created by user {UserId}", puzzle.Id, userId);

            return CreatedAtAction(nameof(GetPuzzle), new { puzzleId = puzzle.Id }, new { id = puzzle.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating puzzle");
            return StatusCode(500, new { error = "Failed to create puzzle" });
        }
    }

    /// <summary>
    /// Update a puzzle (requires puzzle owner)
    /// </summary>
    [HttpPut("{puzzleId}")]
    [RequirePuzzleOwner]
    public async Task<IActionResult> UpdatePuzzle(Guid puzzleId, [FromBody] UpdatePuzzleRequest request)
    {
        try
        {
            var puzzle = await _puzzleRepository.GetPuzzleByIdAsync(puzzleId);
            if (puzzle == null)
            {
                return NotFound(new { error = "Puzzle not found" });
            }

            puzzle.Title = request.Title ?? puzzle.Title;
            puzzle.Description = request.Description ?? puzzle.Description;
            puzzle.Category = request.Category ?? puzzle.Category;
            puzzle.Tags = request.Tags ?? puzzle.Tags;
            puzzle.IsPublic = request.IsPublic ?? puzzle.IsPublic;
            puzzle.UpdatedAt = DateTime.UtcNow;

            await _puzzleRepository.UpdatePuzzleAsync(puzzle);

            _logger.LogInformation("Puzzle {PuzzleId} updated", puzzleId);

            return Ok(new { message = "Puzzle updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating puzzle {PuzzleId}", puzzleId);
            return StatusCode(500, new { error = "Failed to update puzzle" });
        }
    }

    /// <summary>
    /// Delete a puzzle (requires puzzle owner or admin)
    /// </summary>
    [HttpDelete("{puzzleId}")]
    [Authorize]
    public async Task<IActionResult> DeletePuzzle(Guid puzzleId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            var puzzle = await _puzzleRepository.GetPuzzleByIdAsync(puzzleId);
            if (puzzle == null)
            {
                return NotFound(new { error = "Puzzle not found" });
            }

            // Check if user is owner or admin
            if (!isAdmin && puzzle.CreatedByUserId.ToString() != userId)
            {
                return Forbid();
            }

            await _puzzleRepository.DeletePuzzleAsync(puzzleId);

            _logger.LogInformation("Puzzle {PuzzleId} deleted by user {UserId}", puzzleId, userId);

            return Ok(new { message = "Puzzle deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting puzzle {PuzzleId}", puzzleId);
            return StatusCode(500, new { error = "Failed to delete puzzle" });
        }
    }

    /// <summary>
    /// Get user's puzzles (requires authentication)
    /// </summary>
    [HttpGet("my-puzzles")]
    [Authorize]
    public async Task<IActionResult> GetMyPuzzles([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var puzzles = await _puzzleRepository.GetPuzzlesByUserAsync(
                Guid.Parse(userId), 
                (page - 1) * pageSize, 
                pageSize);

            return Ok(new
            {
                puzzles = puzzles.Select(p => new PuzzleDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    PieceCount = p.PieceCount,
                    Difficulty = p.Difficulty.ToString(),
                    Category = p.Category,
                    CreatedAt = p.CreatedAt,
                    AverageRating = p.AverageRating,
                    TotalSessions = p.TotalSessions,
                    IsPublic = p.IsPublic,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUsername = p.CreatedByUser?.Username ?? "You",
                    PiecesDataUrl = p.PiecesDataUrl,
                    Width = p.Width,
                    Height = p.Height,
                    GridColumns = p.GridColumns,
                    GridRows = p.GridRows,
                    EstimatedCompletionMinutes = p.EstimatedCompletionMinutes,
                    Tags = p.Tags,
                    IsFeatured = p.IsFeatured,
                    TotalCompletions = p.TotalCompletions,
                    TotalRatings = p.TotalRatings
                }),
                page,
                pageSize,
                hasMore = puzzles.Count() == pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user puzzles");
            return StatusCode(500, new { error = "Failed to retrieve user puzzles" });
        }
    }

}