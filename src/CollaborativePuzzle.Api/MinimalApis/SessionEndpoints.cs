using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
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
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/sessions")
            .WithTags("Sessions")
            .WithOpenApi()
            .RequireAuthorization()
            .RequireRateLimiting("fixed")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1, 0);

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

        // V2 endpoints with enhanced features
        var v2Group = app.MapGroup("/api/v{version:apiVersion}/sessions")
            .WithTags("Sessions V2")
            .WithOpenApi()
            .RequireAuthorization()
            .RequireRateLimiting("sliding")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(2, 0);

        // GET /api/v2/sessions with enhanced filtering
        v2Group.MapGet("/", SessionEndpointsV2.GetPublicSessionsV2Async)
            .WithName("GetPublicSessionsV2")
            .WithSummary("Get public sessions with enhanced filtering")
            .WithDescription("Returns sessions with advanced filtering and real-time status")
            .Produces<SessionListResponseV2>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // POST /api/v2/sessions/bulk-join
        v2Group.MapPost("/bulk-join", SessionEndpointsV2.BulkJoinSessionsAsync)
            .WithName("BulkJoinSessions")
            .WithSummary("Join multiple sessions")
            .WithDescription("Join multiple sessions in a single request")
            .Produces<BulkJoinResponse>(StatusCodes.Status200OK);

        // GET /api/v2/sessions/{id}/analytics
        v2Group.MapGet("/{id:guid}/analytics", SessionEndpointsV2.GetSessionAnalyticsAsync)
            .WithName("GetSessionAnalytics")
            .WithSummary("Get session analytics")
            .WithDescription("Returns detailed analytics for a session (host only)")
            .Produces<SessionAnalytics>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<Ok<SessionListResponse>> GetPublicSessionsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        ISessionRepository sessionRepository = null!,
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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
        ILogger logger = null!)
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

// V2 Methods Implementation
public static class SessionEndpointsV2
{
    public static async Task<Ok<SessionListResponseV2>> GetPublicSessionsV2Async(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? puzzleCategory = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? status = null,
        [FromQuery] int? minParticipants = null,
        [FromQuery] int? maxParticipants = null,
        [FromQuery] string? sortBy = "created",
        [FromQuery] string? sortOrder = "desc",
        ISessionRepository sessionRepository = null!,
        ILogger logger = null!)
    {
        var skip = (page - 1) * pageSize;
        // In a real implementation, this would use enhanced filtering
        var sessions = await sessionRepository.GetPublicSessionsAsync(skip, pageSize);
        
        var response = new SessionListResponseV2
        {
            Sessions = sessions.Select(s => MapToSessionDtoV2(s)),
            Page = page,
            PageSize = pageSize,
            TotalCount = sessions.Count(), // In real implementation, get total count
            HasMore = sessions.Count() == pageSize,
            SortBy = sortBy ?? "created",
            SortOrder = sortOrder ?? "desc",
            Filters = new SessionFilters
            {
                PuzzleCategory = puzzleCategory,
                Difficulty = difficulty,
                Status = status,
                MinParticipants = minParticipants,
                MaxParticipants = maxParticipants
            }
        };
        
        logger.LogInformation("Retrieved {Count} public sessions with V2 filtering", sessions.Count());
        return TypedResults.Ok(response);
    }

    public static async Task<Ok<BulkJoinResponse>> BulkJoinSessionsAsync(
        BulkJoinRequest request,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        ILogger logger = null!)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return TypedResults.Ok(new BulkJoinResponse
            {
                Success = false,
                Results = new List<BulkJoinResult>()
            });
        }
        
        var results = new List<BulkJoinResult>();
        
        foreach (var sessionId in request.SessionIds)
        {
            try
            {
                var session = await sessionRepository.GetSessionWithParticipantsAsync(sessionId);
                if (session == null)
                {
                    results.Add(new BulkJoinResult
                    {
                        SessionId = sessionId,
                        Success = false,
                        Error = "Session not found"
                    });
                    continue;
                }
                
                // Check if already joined
                var existingParticipant = await sessionRepository.GetParticipantAsync(sessionId, Guid.Parse(userId));
                if (existingParticipant != null)
                {
                    results.Add(new BulkJoinResult
                    {
                        SessionId = sessionId,
                        Success = true,
                        Message = "Already joined"
                    });
                    continue;
                }
                
                // Check max participants
                if (session.Participants?.Count >= session.MaxParticipants)
                {
                    results.Add(new BulkJoinResult
                    {
                        SessionId = sessionId,
                        Success = false,
                        Error = "Session is full"
                    });
                    continue;
                }
                
                await sessionRepository.AddParticipantAsync(sessionId, Guid.Parse(userId));
                results.Add(new BulkJoinResult
                {
                    SessionId = sessionId,
                    Success = true,
                    Message = "Successfully joined"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error joining session {SessionId}", sessionId);
                results.Add(new BulkJoinResult
                {
                    SessionId = sessionId,
                    Success = false,
                    Error = "Internal error"
                });
            }
        }
        
        logger.LogInformation("User {UserId} bulk joined {Count} sessions", userId, results.Count(r => r.Success));
        
        return TypedResults.Ok(new BulkJoinResponse
        {
            Success = true,
            Results = results
        });
    }

    public static async Task<Results<Ok<SessionAnalytics>, NotFound, ForbidHttpResult>> GetSessionAnalyticsAsync(
        Guid id,
        ClaimsPrincipal user,
        ISessionRepository sessionRepository = null!,
        ILogger logger = null!)
    {
        var session = await sessionRepository.GetSessionWithParticipantsAsync(id);
        if (session == null)
        {
            return TypedResults.NotFound();
        }
        
        // Check if user is host
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (session.CreatedByUserId.ToString() != userId)
        {
            logger.LogWarning("User {UserId} attempted to view analytics for session {SessionId} without being host", userId, id);
            return TypedResults.Forbid();
        }
        
        // Generate analytics (simplified for now)
        var analytics = new SessionAnalytics
        {
            SessionId = id,
            TotalParticipants = session.Participants?.Count ?? 0,
            ActiveParticipants = session.Participants?.Count(p => p.Status == Core.Enums.ParticipantStatus.Online) ?? 0,
            TotalPiecesPlaced = session.Participants?.Sum(p => p.PiecesPlaced) ?? 0,
            AveragePiecesPerParticipant = session.Participants?.Any() == true 
                ? (double)session.Participants.Sum(p => p.PiecesPlaced) / session.Participants.Count 
                : 0,
            SessionDuration = session.StartedAt.HasValue 
                ? DateTime.UtcNow - session.StartedAt.Value 
                : TimeSpan.Zero,
            CompletionPercentage = (double)session.CompletionPercentage,
            ParticipantBreakdown = session.Participants?.GroupBy(p => p.Role)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()) ?? new Dictionary<string, int>(),
            ActivityTimeline = new List<ActivityEvent>() // Would be populated from event log
        };
        
        logger.LogInformation("Retrieved analytics for session {SessionId}", id);
        return TypedResults.Ok(analytics);
    }

    private static SessionDtoV2 MapToSessionDtoV2(PuzzleSession session)
    {
        return new SessionDtoV2
        {
            Id = session.Id,
            PuzzleId = session.PuzzleId,
            PuzzleTitle = session.Puzzle?.Title ?? "Unknown",
            PuzzleThumbnailUrl = session.Puzzle?.ImageUrl, // In real implementation, use thumbnail
            Name = session.Name,
            JoinCode = session.JoinCode,
            IsPublic = session.IsPublic,
            MaxParticipants = session.MaxParticipants,
            CurrentParticipants = session.Participants?.Count ?? 0,
            OnlineParticipants = session.Participants?.Count(p => p.Status == Core.Enums.ParticipantStatus.Online) ?? 0,
            Status = session.Status.ToString(),
            StatusDetail = GetStatusDetail(session),
            CreatedAt = session.CreatedAt,
            StartedAt = session.StartedAt,
            LastActivityAt = session.LastActivityAt,
            CompletionPercentage = (double)session.CompletionPercentage,
            EstimatedTimeRemaining = CalculateEstimatedTimeRemaining(session),
            Tags = new[] { session.Puzzle?.Category ?? "general" }
        };
    }

    private static string GetStatusDetail(PuzzleSession session)
    {
        return session.Status switch
        {
            Core.Enums.SessionStatus.Active when session.CompletionPercentage > 75 => "Nearly complete",
            Core.Enums.SessionStatus.Active when session.CompletionPercentage > 50 => "Making good progress",
            Core.Enums.SessionStatus.Active when session.CompletionPercentage > 25 => "In progress",
            Core.Enums.SessionStatus.Active => "Just started",
            Core.Enums.SessionStatus.Completed => "Puzzle completed!",
            Core.Enums.SessionStatus.Paused => "Session paused",
            _ => session.Status.ToString()
        };
    }

    private static TimeSpan? CalculateEstimatedTimeRemaining(PuzzleSession session)
    {
        if (!session.StartedAt.HasValue || session.CompletionPercentage <= 0)
            return null;
            
        var elapsed = DateTime.UtcNow - session.StartedAt.Value;
        var totalEstimated = elapsed.TotalMinutes / ((double)session.CompletionPercentage / 100.0);
        var remaining = totalEstimated - elapsed.TotalMinutes;
        
        return remaining > 0 ? TimeSpan.FromMinutes(remaining) : TimeSpan.Zero;
    }
}

// V2 DTOs
/// <summary>
/// Enhanced session list response for V2
/// </summary>
public class SessionListResponseV2
{
    public IEnumerable<SessionDtoV2> Sessions { get; set; } = new List<SessionDtoV2>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    public string SortBy { get; set; } = "created";
    public string SortOrder { get; set; } = "desc";
    public SessionFilters Filters { get; set; } = default!;
}

/// <summary>
/// Enhanced session DTO for V2
/// </summary>
public class SessionDtoV2 : Core.DTOs.SessionDto
{
    public string PuzzleTitle { get; set; } = default!;
    public string? PuzzleThumbnailUrl { get; set; }
    public int OnlineParticipants { get; set; }
    public string StatusDetail { get; set; } = default!;
    public DateTime? StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public double CompletionPercentage { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Session filters for V2
/// </summary>
public class SessionFilters
{
    public string? PuzzleCategory { get; set; }
    public string? Difficulty { get; set; }
    public string? Status { get; set; }
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
}

/// <summary>
/// Bulk join request
/// </summary>
public class BulkJoinRequest
{
    public Guid[] SessionIds { get; set; } = Array.Empty<Guid>();
}

/// <summary>
/// Bulk join response
/// </summary>
public class BulkJoinResponse
{
    public bool Success { get; set; }
    public IEnumerable<BulkJoinResult> Results { get; set; } = new List<BulkJoinResult>();
}

/// <summary>
/// Individual bulk join result
/// </summary>
public class BulkJoinResult
{
    public Guid SessionId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Session analytics
/// </summary>
public class SessionAnalytics
{
    public Guid SessionId { get; set; }
    public int TotalParticipants { get; set; }
    public int ActiveParticipants { get; set; }
    public int TotalPiecesPlaced { get; set; }
    public double AveragePiecesPerParticipant { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public double CompletionPercentage { get; set; }
    public Dictionary<string, int> ParticipantBreakdown { get; set; } = new();
    public IEnumerable<ActivityEvent> ActivityTimeline { get; set; } = new List<ActivityEvent>();
}

/// <summary>
/// Activity event for analytics
/// </summary>
public class ActivityEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid? UserId { get; set; }
}