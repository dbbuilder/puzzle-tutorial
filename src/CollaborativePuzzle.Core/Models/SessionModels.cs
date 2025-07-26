using System;
using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Data Transfer Object for creating a new puzzle session
    /// </summary>
    public class CreateSessionRequest
    {
        [Required]
        public Guid PuzzleId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(500)]
        public string? Password { get; set; }
        
        [Range(2, 50)]
        public int MaxParticipants { get; set; } = 20;
        
        public bool AllowGuestUsers { get; set; } = true;
        public bool IsVoiceChatEnabled { get; set; } = true;
        public bool IsPublic { get; set; } = true;
        public bool AllowPieceRotation { get; set; } = true;
        public bool ShowPieceOutlines { get; set; }
        public bool EnableSnapToGrid { get; set; } = true;
        
        [Range(5, 100)]
        public int SnapThreshold { get; set; } = 20;
        
        [Range(1, 60)]
        public int PieceLockTimeoutMinutes { get; set; } = 5;
        
        [Range(1, 168)]
        public int InactivityTimeoutHours { get; set; } = 24;
    }
    
    /// <summary>
    /// Data Transfer Object for session information returned to clients
    /// </summary>
    public class SessionDto
    {
        public Guid Id { get; set; }
        public Guid PuzzleId { get; set; }
        public string PuzzleTitle { get; set; } = string.Empty;
        public string PuzzleImageUrl { get; set; } = string.Empty;
        public Guid CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string JoinCode { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }
        public bool AllowGuestUsers { get; set; }
        public bool IsVoiceChatEnabled { get; set; }
        public bool IsPublic { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CompletedPieces { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int TotalMoves { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public bool AllowPieceRotation { get; set; }
        public bool ShowPieceOutlines { get; set; }
        public bool EnableSnapToGrid { get; set; }
        public int SnapThreshold { get; set; }
        public int PieceLockTimeoutMinutes { get; set; }
    }
}
