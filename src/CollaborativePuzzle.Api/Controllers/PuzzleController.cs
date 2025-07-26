using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
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
    private readonly ILogger<PuzzleController> _logger;

    public PuzzleController(
        IPuzzleRepository puzzleRepository,
        IPieceRepository pieceRepository,
        ILogger<PuzzleController> logger)
    {
        _puzzleRepository = puzzleRepository;
        _pieceRepository = pieceRepository;
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
                    Name = p.Name,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    PieceCount = p.PieceCount,
                    Difficulty = p.Difficulty,
                    Category = p.Category?.Name,
                    CreatedAt = p.CreatedAt,
                    Rating = p.AverageRating,
                    TotalPlays = p.TotalSessions
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

            return Ok(new PuzzleDto
            {
                Id = puzzle.Id,
                Name = puzzle.Name,
                Description = puzzle.Description,
                ImageUrl = puzzle.ImageUrl,
                PieceCount = puzzle.PieceCount,
                Difficulty = puzzle.Difficulty,
                Category = puzzle.Category?.Name,
                CreatedAt = puzzle.CreatedAt,
                Rating = puzzle.AverageRating,
                TotalPlays = puzzle.TotalSessions,
                Pieces = puzzle.Pieces?.Select(p => new PuzzlePieceDto
                {
                    Id = p.Id,
                    PieceIndex = p.PieceIndex,
                    CorrectPositionX = p.CorrectPositionX,
                    CorrectPositionY = p.CorrectPositionY,
                    Width = p.Width,
                    Height = p.Height,
                    EdgeData = p.EdgeData
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
                Name = request.Name,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                PieceCount = request.PieceCount,
                Difficulty = request.Difficulty,
                CreatedByUserId = Guid.Parse(userId),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = request.IsPublic
            };

            // Generate puzzle pieces
            var pieces = GeneratePuzzlePieces(puzzle.Id, request.PieceCount, request.Columns, request.Rows);
            
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
            var puzzle = await _puzzleRepository.GetPuzzleAsync(puzzleId);
            if (puzzle == null)
            {
                return NotFound(new { error = "Puzzle not found" });
            }

            puzzle.Name = request.Name ?? puzzle.Name;
            puzzle.Description = request.Description ?? puzzle.Description;
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

            var puzzle = await _puzzleRepository.GetPuzzleAsync(puzzleId);
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
                    Name = p.Name,
                    Description = p.Description,
                    ImageUrl = p.ImageUrl,
                    PieceCount = p.PieceCount,
                    Difficulty = p.Difficulty,
                    Category = p.Category?.Name,
                    CreatedAt = p.CreatedAt,
                    Rating = p.AverageRating,
                    TotalPlays = p.TotalSessions,
                    IsPublic = p.IsPublic
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

    private IEnumerable<PuzzlePiece> GeneratePuzzlePieces(Guid puzzleId, int pieceCount, int columns, int rows)
    {
        var pieces = new List<PuzzlePiece>();
        var pieceWidth = 100.0 / columns;
        var pieceHeight = 100.0 / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var index = row * columns + col;
                pieces.Add(new PuzzlePiece
                {
                    Id = Guid.NewGuid(),
                    PuzzleId = puzzleId,
                    PieceIndex = index,
                    CorrectPositionX = col * pieceWidth,
                    CorrectPositionY = row * pieceHeight,
                    Width = pieceWidth,
                    Height = pieceHeight,
                    IsEdgePiece = row == 0 || row == rows - 1 || col == 0 || col == columns - 1,
                    EdgeData = GenerateEdgeData(row, col, rows, columns),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return pieces;
    }

    private byte[] GenerateEdgeData(int row, int col, int rows, int cols)
    {
        // Simplified edge data generation
        // In a real implementation, this would contain the actual edge shape data
        return new byte[] { (byte)row, (byte)col, (byte)rows, (byte)cols };
    }
}

public class UpdatePuzzleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsPublic { get; set; }
}

public class PuzzlePieceDto
{
    public Guid Id { get; set; }
    public int PieceIndex { get; set; }
    public double CorrectPositionX { get; set; }
    public double CorrectPositionY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public byte[]? EdgeData { get; set; }
}