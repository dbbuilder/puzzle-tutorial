using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents a user in the collaborative puzzle platform
    /// </summary>
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(255)]
        public string? DisplayName { get; set; }
        
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }
        
        [MaxLength(50)]
        public string? ExternalId { get; set; }
        
        [MaxLength(100)]
        public string? Provider { get; set; }
        
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // User preferences
        public bool AllowVoiceChat { get; set; } = true;
        public bool AllowNotifications { get; set; } = true;
        public string? PreferredLanguage { get; set; } = "en";

        // Statistics
        public int TotalPuzzlesCreated { get; set; }
        public int TotalPuzzlesCompleted { get; set; }
        public int TotalSessionsJoined { get; set; }
        public TimeSpan TotalActiveTime { get; set; } = TimeSpan.Zero;
        
        // Navigation properties
        public virtual ICollection<Puzzle> CreatedPuzzles { get; set; } = new List<Puzzle>();
        public virtual ICollection<PuzzleSession> CreatedSessions { get; set; } = new List<PuzzleSession>();
        public virtual ICollection<SessionParticipant> SessionParticipants { get; set; } = new List<SessionParticipant>();
        public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<PuzzlePiece> LockedPieces { get; set; } = new List<PuzzlePiece>();
    }
}
