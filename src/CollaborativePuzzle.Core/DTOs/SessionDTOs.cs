using System;
using System.Collections.Generic;

namespace CollaborativePuzzle.Core.DTOs;

/// <summary>
/// DTO for session list response
/// </summary>
public class SessionListResponse
{
    public IEnumerable<SessionDto> Sessions { get; set; } = new List<SessionDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// DTO for session information
/// </summary>
public class SessionDto
{
    public Guid Id { get; set; }
    public Guid PuzzleId { get; set; }
    public string Name { get; set; } = default!;
    public string JoinCode { get; set; } = default!;
    public bool IsPublic { get; set; }
    public int MaxParticipants { get; set; }
    public int CurrentParticipants { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for detailed session information
/// </summary>
public class SessionDetailsDto : SessionDto
{
    public string PuzzleName { get; set; } = default!;
    public string? PuzzleImageUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CompletedPieces { get; set; }
    public int TotalPieces { get; set; }
    public decimal CompletionPercentage { get; set; }
}

/// <summary>
/// Request to create a new session
/// </summary>
public class CreateSessionRequest
{
    public Guid PuzzleId { get; set; }
    public string? Name { get; set; }
    public bool? IsPublic { get; set; }
    public int? MaxParticipants { get; set; }
}

/// <summary>
/// Request to update session settings
/// </summary>
public class UpdateSessionRequest
{
    public string? Name { get; set; }
    public bool? IsPublic { get; set; }
    public int? MaxParticipants { get; set; }
}