using System;
using System.ComponentModel.DataAnnotations;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents a chat message within a puzzle session
    /// </summary>
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid SessionId { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;
        
        public MessageType Type { get; set; } = MessageType.Text;
        
        // Optional metadata for system messages (JSON format)
        public string? Metadata { get; set; }
        
        // Message threading support
        public Guid? ReplyToMessageId { get; set; }

        // Message reactions and interactions
        public int LikeCount { get; set; }
        public bool IsPinned { get; set; }
        public bool IsEdited { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }

        // Moderation flags
        public bool IsDeleted { get; set; }
        public bool IsHidden { get; set; }
        public Guid? DeletedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletionReason { get; set; }
        
        // Message formatting and content
        [MaxLength(50)]
        public string? Language { get; set; }

        public bool ContainsMention { get; set; }
        public bool ContainsLink { get; set; }

        // Navigation properties
        public virtual PuzzleSession Session { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual ChatMessage? ReplyToMessage { get; set; }
        public virtual User? DeletedByUser { get; set; }
    }
}
