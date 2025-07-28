using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.MinimalApis;

/// <summary>
/// Minimal API endpoints for puzzle management
/// </summary>
public static class PuzzleEndpoints
{
    public static void MapPuzzleEndpoints(this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        // V1 endpoints
        var v1Group = app.MapGroup("/api/v{version:apiVersion}/puzzles")
            .WithTags("Puzzles")
            .WithOpenApi()
            .RequireAuthorization()
            .RequireRateLimiting("fixed")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1, 0);

        // GET /api/v1/puzzles
        v1Group.MapGet("/", GetPuzzlesAsync)
            .WithName("GetPuzzles")
            .WithSummary("Get all public puzzles")
            .WithDescription("Returns a paginated list of public puzzles with optional filtering")
            .Produces<PuzzleListResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // GET /api/v1/puzzles/{id}
        v1Group.MapGet("/{id:guid}", GetPuzzleByIdAsync)
            .WithName("GetPuzzleById")
            .WithSummary("Get a puzzle by ID")
            .WithDescription("Returns detailed information about a specific puzzle")
            .Produces<PuzzleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        // POST /api/v1/puzzles
        v1Group.MapPost("/", CreatePuzzleAsync)
            .WithName("CreatePuzzle")
            .WithSummary("Create a new puzzle")
            .WithDescription("Creates a new puzzle with the provided image and settings")
            .Produces<PuzzleDto>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireAuthorization("RequireUserRole");

        // PUT /api/v1/puzzles/{id}
        v1Group.MapPut("/{id:guid}", UpdatePuzzleAsync)
            .WithName("UpdatePuzzle")
            .WithSummary("Update a puzzle")
            .WithDescription("Updates puzzle metadata (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        // DELETE /api/v1/puzzles/{id}
        v1Group.MapDelete("/{id:guid}", DeletePuzzleAsync)
            .WithName("DeletePuzzle")
            .WithSummary("Delete a puzzle")
            .WithDescription("Permanently deletes a puzzle and all associated data (owner only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization();

        // GET /api/v1/puzzles/{id}/sessions
        v1Group.MapGet("/{id:guid}/sessions", GetPuzzleSessionsAsync)
            .WithName("GetPuzzleSessions")
            .WithSummary("Get active sessions for a puzzle")
            .WithDescription("Returns all active sessions for a specific puzzle")
            .Produces<SessionListResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // GET /api/v1/puzzles/search
        v1Group.MapGet("/search", SearchPuzzlesAsync)
            .WithName("SearchPuzzles")
            .WithSummary("Search puzzles")
            .WithDescription("Search puzzles by title and description")
            .Produces<PuzzleListResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // GET /api/v1/puzzles/my
        v1Group.MapGet("/my", GetMyPuzzlesAsync)
            .WithName("GetMyPuzzles")
            .WithSummary("Get user's puzzles")
            .WithDescription("Returns all puzzles created by the authenticated user")
            .Produces<PuzzleListResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();

        // V2 endpoints with enhanced features
        var v2Group = app.MapGroup("/api/v{version:apiVersion}/puzzles")
            .WithTags("Puzzles V2")
            .WithOpenApi()
            .RequireAuthorization()
            .RequireRateLimiting("sliding")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(2, 0);

        // GET /api/v2/puzzles with enhanced filtering
        v2Group.MapGet("/", GetPuzzlesV2Async)
            .WithName("GetPuzzlesV2")
            .WithSummary("Get all public puzzles with enhanced filtering")
            .WithDescription("Returns a paginated list of public puzzles with enhanced filtering and sorting options")
            .Produces<PuzzleListResponseV2>(StatusCodes.Status200OK)
            .AllowAnonymous();
    }

    private static async Task<Ok<PuzzleListResponse>> GetPuzzlesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? difficulty = null,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        var skip = (page - 1) * pageSize;
        var puzzles = await puzzleRepository.GetPublicPuzzlesAsync(skip, pageSize, category, difficulty);
        
        var response = new PuzzleListResponse
        {
            Puzzles = puzzles.Select(p => MapToPuzzleDto(p)),
            Page = page,
            PageSize = pageSize,
            HasMore = puzzles.Count() == pageSize
        };
        
        logger.LogInformation("Retrieved {Count} public puzzles", puzzles.Count());
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PuzzleDto>, NotFound>> GetPuzzleByIdAsync(
        Guid id,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        var puzzle = await puzzleRepository.GetPuzzleByIdAsync(id);
        if (puzzle == null)
        {
            logger.LogWarning("Puzzle {PuzzleId} not found", id);
            return TypedResults.NotFound();
        }
        
        return TypedResults.Ok(MapToPuzzleDto(puzzle));
    }

    private static async Task<Results<Created<PuzzleDto>, BadRequest<ValidationProblemDetails>>> CreatePuzzleAsync(
        CreatePuzzleRequest request,
        ClaimsPrincipal user,
        IPuzzleRepository puzzleRepository = null!,
        IPuzzleGeneratorService puzzleGeneratorService = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "User not authenticated",
                Status = StatusCodes.Status400BadRequest
            });
        }
        
        var puzzle = new Puzzle
        {
            Title = request.Title,
            Description = request.Description,
            PieceCount = request.PieceCount,
            Difficulty = PuzzleDifficulty.Medium, // Default difficulty
            ImageUrl = request.ImageUrl,
            CreatedByUserId = Guid.Parse(userId),
            IsPublic = request.IsPublic
        };
        
        // Generate puzzle pieces using the generator service
        var pieces = await puzzleGeneratorService.GeneratePuzzlePiecesAsync(
            puzzle.ImageUrl,
            puzzle.PieceCount,
            puzzle.Id,
            puzzle.Width,
            puzzle.Height
        );
        
        var created = await puzzleRepository.CreatePuzzleAsync(puzzle, pieces);
        logger.LogInformation("Created puzzle {PuzzleId} for user {UserId}", created.Id, userId);
        
        return TypedResults.Created($"/api/v1/puzzles/{created.Id}", MapToPuzzleDto(created));
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> UpdatePuzzleAsync(
        Guid id,
        UpdatePuzzleRequest request,
        ClaimsPrincipal user,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        var puzzle = await puzzleRepository.GetPuzzleByIdAsync(id);
        if (puzzle == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check ownership
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (puzzle.CreatedByUserId.ToString() != userId)
        {
            logger.LogWarning("User {UserId} attempted to update puzzle {PuzzleId} without ownership", userId, id);
            return TypedResults.Forbid();
        }
        
        // Update fields
        if (!string.IsNullOrWhiteSpace(request.Title))
            puzzle.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.Description))
            puzzle.Description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.Category))
            puzzle.Category = request.Category;
        if (!string.IsNullOrWhiteSpace(request.Tags))
            puzzle.Tags = request.Tags;
        if (request.IsPublic.HasValue)
            puzzle.IsPublic = request.IsPublic.Value;
        
        puzzle.UpdatedAt = DateTime.UtcNow;
        
        await puzzleRepository.UpdatePuzzleAsync(puzzle);
        logger.LogInformation("Updated puzzle {PuzzleId}", id);
        
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ValidationProblemDetails>>> DeletePuzzleAsync(
        Guid id,
        ClaimsPrincipal user,
        IPuzzleRepository puzzleRepository = null!,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var puzzle = await puzzleRepository.GetPuzzleByIdAsync(id);
        if (puzzle == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check ownership
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (puzzle.CreatedByUserId.ToString() != userId)
        {
            logger.LogWarning("User {UserId} attempted to delete puzzle {PuzzleId} without ownership", userId, id);
            return TypedResults.Forbid();
        }
        
        // Check for active sessions
        var activeSessions = await sessionRepository.GetActiveSessionsForPuzzleAsync(id);
        if (activeSessions.Any())
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "Cannot delete puzzle with active sessions",
                Status = StatusCodes.Status400BadRequest
            });
        }
        
        await puzzleRepository.DeletePuzzleAsync(id);
        logger.LogInformation("Deleted puzzle {PuzzleId}", id);
        
        return TypedResults.NoContent();
    }

    private static async Task<Ok<SessionListResponse>> GetPuzzleSessionsAsync(
        Guid id,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var sessions = await sessionRepository.GetActiveSessionsForPuzzleAsync(id);
        
        var response = new SessionListResponse
        {
            Sessions = sessions.Select(s => new Core.DTOs.SessionDto
            {
                Id = s.Id,
                PuzzleId = s.PuzzleId,
                Name = s.Name,
                JoinCode = s.JoinCode,
                IsPublic = s.IsPublic,
                MaxParticipants = s.MaxParticipants,
                CurrentParticipants = s.Participants?.Count ?? 0,
                Status = s.Status.ToString(),
                CreatedAt = s.CreatedAt
            })
        };
        
        logger.LogInformation("Retrieved {Count} sessions for puzzle {PuzzleId}", sessions.Count(), id);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<PuzzleListResponse>> SearchPuzzlesAsync(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return TypedResults.Ok(new PuzzleListResponse { Puzzles = Enumerable.Empty<PuzzleDto>() });
        }
        
        var skip = (page - 1) * pageSize;
        var puzzles = await puzzleRepository.SearchPuzzlesAsync(q, skip, pageSize);
        
        var response = new PuzzleListResponse
        {
            Puzzles = puzzles.Select(p => MapToPuzzleDto(p)),
            Page = page,
            PageSize = pageSize,
            HasMore = puzzles.Count() == pageSize
        };
        
        logger.LogInformation("Search for '{Query}' returned {Count} puzzles", q, puzzles.Count());
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<PuzzleListResponse>> GetMyPuzzlesAsync(
        ClaimsPrincipal user,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Ok(new PuzzleListResponse { Puzzles = Enumerable.Empty<PuzzleDto>() });
        }
        
        var skip = (page - 1) * pageSize;
        var puzzles = await puzzleRepository.GetPuzzlesByUserAsync(Guid.Parse(userId), skip, pageSize);
        
        var response = new PuzzleListResponse
        {
            Puzzles = puzzles.Select(p => MapToPuzzleDto(p)),
            Page = page,
            PageSize = pageSize,
            HasMore = puzzles.Count() == pageSize
        };
        
        logger.LogInformation("Retrieved {Count} puzzles for user {UserId}", puzzles.Count(), userId);
        return TypedResults.Ok(response);
    }

    private static PuzzleDto MapToPuzzleDto(Puzzle puzzle)
    {
        return new PuzzleDto
        {
            Id = puzzle.Id,
            Title = puzzle.Title,
            Description = puzzle.Description,
            PieceCount = puzzle.PieceCount,
            Difficulty = puzzle.Difficulty.ToString(),
            ImageUrl = puzzle.ImageUrl,
            PiecesDataUrl = puzzle.PiecesDataUrl,
            Category = puzzle.Category,
            Tags = puzzle.Tags,
            IsPublic = puzzle.IsPublic,
            CreatedByUserId = puzzle.CreatedByUserId,
            CreatedAt = puzzle.CreatedAt,
            TotalSessions = puzzle.TotalSessions,
            TotalCompletions = puzzle.TotalCompletions
        };
    }

    private static IEnumerable<PuzzlePiece> GeneratePuzzlePieces(Guid puzzleId, int pieceCount)
    {
        // Simplified piece generation - in production, this would analyze the image
        var pieces = new List<PuzzlePiece>();
        var cols = (int)Math.Sqrt(pieceCount);
        var rows = pieceCount / cols;
        
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                pieces.Add(new PuzzlePiece
                {
                    Id = Guid.NewGuid(),
                    PuzzleId = puzzleId,
                    PieceNumber = row * cols + col,
                    GridX = col,
                    GridY = row,
                    CorrectX = col * 100,
                    CorrectY = row * 100,
                    ImageX = col * 100,
                    ImageY = row * 100,
                    ImageWidth = 100,
                    ImageHeight = 100,
                    ShapeData = "{}" // Placeholder for shape data
                });
            }
        }
        
        return pieces;
    }

    // V2 Methods
    private static async Task<Ok<PuzzleListResponseV2>> GetPuzzlesV2Async(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? sortBy = "created",
        [FromQuery] string? sortOrder = "desc",
        [FromQuery] int? minPieces = null,
        [FromQuery] int? maxPieces = null,
        IPuzzleRepository puzzleRepository = null!,
        ILogger<Program> logger = null!)
    {
        var skip = (page - 1) * pageSize;
        // In a real implementation, this would use the enhanced filtering
        var puzzles = await puzzleRepository.GetPublicPuzzlesAsync(skip, pageSize, category, difficulty);
        
        var response = new PuzzleListResponseV2
        {
            Puzzles = puzzles.Select(p => MapToPuzzleDtoV2(p)),
            Page = page,
            PageSize = pageSize,
            TotalCount = puzzles.Count(), // In real implementation, get total count
            HasMore = puzzles.Count() == pageSize,
            SortBy = sortBy ?? "created",
            SortOrder = sortOrder ?? "desc"
        };
        
        logger.LogInformation("Retrieved {Count} public puzzles with V2 filtering", puzzles.Count());
        return TypedResults.Ok(response);
    }

    private static PuzzleDtoV2 MapToPuzzleDtoV2(Puzzle puzzle)
    {
        return new PuzzleDtoV2
        {
            Id = puzzle.Id,
            Title = puzzle.Title,
            Description = puzzle.Description,
            PieceCount = puzzle.PieceCount,
            Difficulty = puzzle.Difficulty.ToString(),
            ImageUrl = puzzle.ImageUrl,
            ThumbnailUrl = puzzle.ImageUrl, // In real implementation, generate thumbnail
            PiecesDataUrl = puzzle.PiecesDataUrl,
            Category = puzzle.Category,
            Tags = puzzle.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            IsPublic = puzzle.IsPublic,
            CreatedByUserId = puzzle.CreatedByUserId,
            CreatedByUsername = puzzle.CreatedByUser?.Username ?? "Unknown",
            CreatedAt = puzzle.CreatedAt,
            TotalSessions = puzzle.TotalSessions,
            TotalCompletions = puzzle.TotalCompletions,
            AverageRating = puzzle.AverageRating,
            TotalRatings = puzzle.TotalRatings,
            EstimatedDuration = TimeSpan.FromMinutes(puzzle.EstimatedCompletionMinutes),
            Dimensions = new PuzzleDimensions
            {
                Width = puzzle.Width,
                Height = puzzle.Height,
                Columns = puzzle.GridColumns,
                Rows = puzzle.GridRows
            }
        };
    }
}

/// <summary>
/// Response containing a list of puzzles
/// </summary>
public class PuzzleListResponse
{
    public IEnumerable<PuzzleDto> Puzzles { get; set; } = new List<PuzzleDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// Enhanced response for V2 with additional metadata
/// </summary>
public class PuzzleListResponseV2
{
    public IEnumerable<PuzzleDtoV2> Puzzles { get; set; } = new List<PuzzleDtoV2>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    public string SortBy { get; set; } = "created";
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Enhanced puzzle DTO for V2 with additional fields
/// </summary>
public class PuzzleDtoV2 : PuzzleDto
{
    public string ThumbnailUrl { get; set; } = default!;
    public new string[] Tags { get; set; } = Array.Empty<string>();
    public new string CreatedByUsername { get; set; } = default!;
    public TimeSpan EstimatedDuration { get; set; }
    public PuzzleDimensions Dimensions { get; set; } = default!;
}

/// <summary>
/// Puzzle dimensions information
/// </summary>
public class PuzzleDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
}