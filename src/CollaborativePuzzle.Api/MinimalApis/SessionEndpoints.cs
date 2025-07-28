using System.Security.Claims;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Enums;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CollaborativePuzzle.Api.MinimalApis;

/// <summary>
/// Minimal API endpoints for session management
/// </summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sessions")
            .WithTags("Sessions")
            .WithOpenApi()
            .RequireAuthorization()
            .RequireRateLimiting("fixed");

        // GET /api/v1/sessions
        group.MapGet("/", GetPublicSessionsAsync)
            .WithName("GetPublicSessions")
            .WithSummary("Get public sessions")
            .WithDescription("Returns a list of public sessions that can be joined")
            .Produces<SessionListResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // GET /api/v1/sessions/{id}
        group.MapGet("/{id:guid}", GetSessionByIdAsync)
            .WithName("GetSessionById")
            .WithSummary("Get session details")
            .WithDescription("Returns detailed information about a specific session")
            .Produces<SessionDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/sessions/join/{joinCode}
        group.MapGet("/join/{joinCode}", GetSessionByJoinCodeAsync)
            .WithName("GetSessionByJoinCode")
            .WithSummary("Get session by join code")
            .WithDescription("Returns session information using a join code")
            .Produces<SessionDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        // POST /api/v1/sessions
        group.MapPost("/", CreateSessionAsync)
            .WithName("CreateSession")
            .WithSummary("Create a new session")
            .WithDescription("Creates a new puzzle-solving session")
            .Produces<SessionDetailsDto>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        // POST /api/v1/sessions/{id}/join
        group.MapPost("/{id:guid}/join", JoinSessionAsync)
            .WithName("JoinSession")
            .WithSummary("Join a session")
            .WithDescription("Join an existing puzzle session")
            .Produces<JoinSessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        // POST /api/v1/sessions/{id}/leave
        group.MapPost("/{id:guid}/leave", LeaveSessionAsync)
            .WithName("LeaveSession")
            .WithSummary("Leave a session")
            .WithDescription("Leave the current puzzle session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // PUT /api/v1/sessions/{id}
        group.MapPut("/{id:guid}", UpdateSessionAsync)
            .WithName("UpdateSession")
            .WithSummary("Update session settings")
            .WithDescription("Updates session settings (host only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/sessions/{id}
        group.MapDelete("/{id:guid}", DeleteSessionAsync)
            .WithName("DeleteSession")
            .WithSummary("Delete a session")
            .WithDescription("Ends and deletes a session (host only)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/sessions/{id}/participants
        group.MapGet("/{id:guid}/participants", GetSessionParticipantsAsync)
            .WithName("GetSessionParticipants")
            .WithSummary("Get session participants")
            .WithDescription("Returns all participants in a session")
            .Produces<ParticipantListResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/sessions/my
        group.MapGet("/my", GetMySessionsAsync)
            .WithName("GetMySessions")
            .WithSummary("Get user's active sessions")
            .WithDescription("Returns all active sessions for the authenticated user")
            .Produces<SessionListResponse>(StatusCodes.Status200OK);
    }

    private static async Task<Ok<SessionListResponse>> GetPublicSessionsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var skip = (page - 1) * pageSize;
        var sessions = await sessionRepository.GetPublicSessionsAsync(skip, pageSize);
        
        var response = new SessionListResponse
        {
            Sessions = sessions.Select(s => MapToSessionDto(s)),
            Page = page,
            PageSize = pageSize,
            HasMore = sessions.Count() == pageSize
        };
        
        logger.LogInformation("Retrieved {Count} public sessions", sessions.Count());
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<SessionDetailsDto>, NotFound>> GetSessionByIdAsync(
        Guid id,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var session = await sessionRepository.GetSessionWithParticipantsAsync(id);
        if (session == null)
        {
            logger.LogWarning("Session {SessionId} not found", id);
            return TypedResults.NotFound();
        }
        
        return TypedResults.Ok(MapToSessionDetailsDto(session));
    }

    private static async Task<Results<Ok<SessionDetailsDto>, NotFound>> GetSessionByJoinCodeAsync(
        string joinCode,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var session = await sessionRepository.GetSessionByJoinCodeAsync(joinCode);
        if (session == null)
        {
            logger.LogWarning("Session with join code {JoinCode} not found", joinCode);
            return TypedResults.NotFound();
        }
        
        return TypedResults.Ok(MapToSessionDetailsDto(session));
    }

    private static async Task<Results<Created<SessionDetailsDto>, BadRequest<ValidationProblemDetails>>> CreateSessionAsync(
        CreateSessionRequest request,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        IPuzzleRepository puzzleRepository = null!,
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
        
        // Verify puzzle exists
        var puzzle = await puzzleRepository.GetPuzzleByIdAsync(request.PuzzleId);
        if (puzzle == null)
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "Puzzle not found",
                Status = StatusCodes.Status400BadRequest
            });
        }
        
        var session = new PuzzleSession
        {
            PuzzleId = request.PuzzleId,
            Name = request.Name ?? $"{puzzle.Title} Session",
            CreatedByUserId = Guid.Parse(userId),
            JoinCode = GenerateJoinCode(),
            IsPublic = request.IsPublic ?? true,
            MaxParticipants = request.MaxParticipants ?? 10,
            Status = SessionStatus.Active
        };
        
        var created = await sessionRepository.CreateSessionAsync(session);
        
        // Add creator as participant
        await sessionRepository.AddParticipantAsync(created.Id, Guid.Parse(userId));
        
        logger.LogInformation("Created session {SessionId} for puzzle {PuzzleId}", created.Id, request.PuzzleId);
        
        return TypedResults.Created($"/api/v1/sessions/{created.Id}", MapToSessionDetailsDto(created));
    }

    private static async Task<Results<Ok<JoinSessionResponse>, NotFound, BadRequest<ValidationProblemDetails>>> JoinSessionAsync(
        Guid id,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
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
        
        var session = await sessionRepository.GetSessionWithParticipantsAsync(id);
        if (session == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check if already joined
        var existingParticipant = await sessionRepository.GetParticipantAsync(id, Guid.Parse(userId));
        if (existingParticipant != null)
        {
            return TypedResults.Ok(new JoinSessionResponse
            {
                SessionId = id,
                Message = "Already joined this session"
            });
        }
        
        // Check max participants
        if (session.Participants?.Count >= session.MaxParticipants)
        {
            return TypedResults.BadRequest(new ValidationProblemDetails
            {
                Title = "Session is full",
                Status = StatusCodes.Status400BadRequest
            });
        }
        
        await sessionRepository.AddParticipantAsync(id, Guid.Parse(userId));
        logger.LogInformation("User {UserId} joined session {SessionId}", userId, id);
        
        return TypedResults.Ok(new JoinSessionResponse
        {
            SessionId = id,
            Message = "Successfully joined session"
        });
    }

    private static async Task<Results<NoContent, NotFound>> LeaveSessionAsync(
        Guid id,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.NotFound();
        }
        
        var removed = await sessionRepository.RemoveParticipantAsync(id, Guid.Parse(userId));
        if (!removed)
        {
            return TypedResults.NotFound();
        }
        
        logger.LogInformation("User {UserId} left session {SessionId}", userId, id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> UpdateSessionAsync(
        Guid id,
        UpdateSessionRequest request,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var session = await sessionRepository.GetSessionAsync(id);
        if (session == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check if user is host
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (session.CreatedByUserId.ToString() != userId)
        {
            logger.LogWarning("User {UserId} attempted to update session {SessionId} without being host", userId, id);
            return TypedResults.Forbid();
        }
        
        // Update fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            session.Name = request.Name;
        if (request.IsPublic.HasValue)
            session.IsPublic = request.IsPublic.Value;
        if (request.MaxParticipants.HasValue && request.MaxParticipants.Value >= (session.Participants?.Count ?? 0))
            session.MaxParticipants = request.MaxParticipants.Value;
        
        await sessionRepository.UpdateSessionAsync(session);
        logger.LogInformation("Updated session {SessionId}", id);
        
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteSessionAsync(
        Guid id,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var session = await sessionRepository.GetSessionAsync(id);
        if (session == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check if user is host
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (session.CreatedByUserId.ToString() != userId)
        {
            logger.LogWarning("User {UserId} attempted to delete session {SessionId} without being host", userId, id);
            return TypedResults.Forbid();
        }
        
        await sessionRepository.DeleteSessionAsync(id);
        logger.LogInformation("Deleted session {SessionId}", id);
        
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ParticipantListResponse>, NotFound>> GetSessionParticipantsAsync(
        Guid id,
        ISessionRepository sessionRepository = null!,
        ILogger<Program> logger = null!)
    {
        var session = await sessionRepository.GetSessionAsync(id);
        if (session == null)
        {
            return TypedResults.NotFound();
        }
        
        var participants = await sessionRepository.GetSessionParticipantsAsync(id);
        
        var response = new ParticipantListResponse
        {
            Participants = participants.Select(p => new ParticipantDto
            {
                UserId = p.UserId,
                Username = p.User?.Username ?? "Unknown",
                Role = p.Role.ToString(),
                Status = p.Status.ToString(),
                JoinedAt = p.JoinedAt,
                PiecesPlaced = p.PiecesPlaced
            })
        };
        
        logger.LogInformation("Retrieved {Count} participants for session {SessionId}", participants.Count(), id);
        return TypedResults.Ok(response);
    }

    private static async Task<Ok<SessionListResponse>> GetMySessionsAsync(
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        IUserRepository userRepository = null!,
        ILogger<Program> logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Ok(new SessionListResponse { Sessions = Enumerable.Empty<SessionDto>() });
        }
        
        var userGuid = Guid.Parse(userId);
        var sessions = new List<PuzzleSession>();
        
        // Get sessions created by user
        var createdSessions = await sessionRepository.GetPublicSessionsAsync(0, 100);
        sessions.AddRange(createdSessions.Where(s => s.CreatedByUserId == userGuid));
        
        // TODO: Get sessions user is participating in
        
        var response = new SessionListResponse
        {
            Sessions = sessions.Select(s => MapToSessionDto(s))
        };
        
        logger.LogInformation("Retrieved {Count} sessions for user {UserId}", sessions.Count, userId);
        return TypedResults.Ok(response);
    }

    private static SessionDto MapToSessionDto(PuzzleSession session)
    {
        return new SessionDto
        {
            Id = session.Id,
            PuzzleId = session.PuzzleId,
            Name = session.Name,
            JoinCode = session.JoinCode,
            IsPublic = session.IsPublic,
            MaxParticipants = session.MaxParticipants,
            CurrentParticipants = session.Participants?.Count ?? 0,
            Status = session.Status.ToString(),
            CreatedAt = session.CreatedAt
        };
    }

    private static SessionDetailsDto MapToSessionDetailsDto(PuzzleSession session)
    {
        return new SessionDetailsDto
        {
            Id = session.Id,
            PuzzleId = session.PuzzleId,
            PuzzleName = session.Puzzle?.Title ?? "Unknown Puzzle",
            PuzzleImageUrl = session.Puzzle?.ImageUrl,
            Name = session.Name,
            JoinCode = session.JoinCode,
            IsPublic = session.IsPublic,
            MaxParticipants = session.MaxParticipants,
            CurrentParticipants = session.Participants?.Count ?? 0,
            Status = session.Status.ToString(),
            CreatedByUserId = session.CreatedByUserId,
            CreatedAt = session.CreatedAt,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            CompletedPieces = session.CompletedPieces,
            TotalPieces = session.Puzzle?.PieceCount ?? 0,
            CompletionPercentage = session.CompletionPercentage
        };
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[6];
        rng.GetBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

/// <summary>
/// Response for joining a session
/// </summary>
public class JoinSessionResponse
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = default!;
}

/// <summary>
/// Response containing session participants
/// </summary>
public class ParticipantListResponse
{
    public IEnumerable<ParticipantDto> Participants { get; set; } = new List<ParticipantDto>();
}

/// <summary>
/// Participant information
/// </summary>
public class ParticipantDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime JoinedAt { get; set; }
    public int PiecesPlaced { get; set; }
}