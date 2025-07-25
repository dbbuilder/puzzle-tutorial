using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents an active collaborative puzzle solving session
    /// </summary>
    public class PuzzleSession
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid PuzzleId { get; set; }
        
        [Required]
        public Guid CreatedByUserId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string JoinCode { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Password { get; set; }
        
        public int MaxParticipants { get; set; } = 20;
        public bool AllowGuestUsers { get; set; } = true;
        public bool IsVoiceChatEnabled { get; set; } = true;
        public bool IsPublic { get; set; } = true;
        
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        
        // Progress tracking
        public int CompletedPieces { get; set; } = 0;
        public decimal CompletionPercentage { get; set; } = 0.0m;
        public int TotalMoves { get; set; } = 0;
        
        // Timing information
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        
        // Auto-cleanup after inactivity (in hours)
        public int InactivityTimeoutHours { get; set; } = 24;
        
        // Session settings
        public bool AllowPieceRotation { get; set; } = true;
        public bool ShowPieceOutlines { get; set; } = false;
        public bool EnableSnapToGrid { get; set; } = true;
        public int SnapThreshold { get; set; } = 20;
        
        // Collaboration settings
        public int PieceLockTimeoutMinutes { get; set; } = 5;
        public bool AllowConcurrentEditing { get; set; } = false;
        
        // Version for optimistic concurrency control
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        
        // Navigation properties
        public virtual Puzzle Puzzle { get; set; } = null!;
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    }
}
