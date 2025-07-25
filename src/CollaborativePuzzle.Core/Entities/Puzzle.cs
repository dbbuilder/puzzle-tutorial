using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents a jigsaw puzzle with metadata and piece information
    /// </summary>
    public class Puzzle
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        public Guid CreatedByUserId { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string PiecesDataUrl { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? ImageFileName { get; set; }
        
        [MaxLength(50)]
        public string? ImageContentType { get; set; }
        
        public long ImageSizeBytes { get; set; }
        
        public int PieceCount { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int GridColumns { get; set; }
        public int GridRows { get; set; }
        
        public PuzzleDifficulty Difficulty { get; set; }
        public int EstimatedCompletionMinutes { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
        
        [MaxLength(500)]
        public string? Tags { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        public bool IsPublic { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        
        // Usage statistics
        public int TotalSessions { get; set; } = 0;
        public int TotalCompletions { get; set; } = 0;
        public TimeSpan AverageCompletionTime { get; set; } = TimeSpan.Zero;
        public double AverageRating { get; set; } = 0.0;
        public int TotalRatings { get; set; } = 0;
        
        // Navigation properties
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual ICollection<PuzzlePiece> Pieces { get; set; } = new List<PuzzlePiece>();
        public virtual ICollection<PuzzleSession> Sessions { get; set; } = new List<PuzzleSession>();
    }
}
