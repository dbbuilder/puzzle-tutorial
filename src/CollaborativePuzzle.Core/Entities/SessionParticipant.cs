using System;
using System.ComponentModel.DataAnnotations;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents a user's participation in a puzzle session
    /// </summary>
    public class SessionParticipant
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid SessionId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        public ParticipantRole Role { get; set; } = ParticipantRole.Participant;
        public ParticipantStatus Status { get; set; } = ParticipantStatus.Online;
        
        // Current cursor position for collaborative indicators
        public int? CursorX { get; set; }
        public int? CursorY { get; set; }
        
        // Current view area for optimization
        public int? ViewportX { get; set; }
        public int? ViewportY { get; set; }
        public int? ViewportWidth { get; set; }
        public int? ViewportHeight { get; set; }
        public float? ZoomLevel { get; set; } = 1.0f;
        
        // Voice chat participation
        public bool IsVoiceChatActive { get; set; } = false;
        public bool IsMuted { get; set; } = false;
        public bool IsDeafened { get; set; } = false;
        
        // Participation tracking
        public int PiecesPlaced { get; set; } = 0;
        public int PiecesMoved { get; set; } = 0;
        public int MessagesPosted { get; set; } = 0;
        public TimeSpan TotalActiveTime { get; set; } = TimeSpan.Zero;
        
        // Connection information
        [MaxLength(100)]
        public string? ConnectionId { get; set; }
        
        [MaxLength(45)]
        public string? IpAddress { get; set; }
        
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        
        // Session timing
        public DateTime JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime? LastPingAt { get; set; }
        
        // Permissions
        public bool CanMovePieces { get; set; } = true;
        public bool CanChat { get; set; } = true;
        public bool CanUseVoiceChat { get; set; } = true;
        public bool CanInviteOthers { get; set; } = false;
        
        // Version for optimistic concurrency control
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        
        // Navigation properties
        public virtual PuzzleSession Session { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
