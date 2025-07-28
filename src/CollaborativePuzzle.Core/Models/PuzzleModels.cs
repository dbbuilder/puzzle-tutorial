using System;
using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Data Transfer Object for creating a new puzzle
    /// </summary>
    public class CreatePuzzleRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [Range(100, 5000)]
        public int PieceCount { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
        
        [MaxLength(500)]
        public string? Tags { get; set; }
        
        public bool IsPublic { get; set; } = true;
        
        // Image will be uploaded separately and URL provided
        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Data Transfer Object for puzzle information returned to clients
    /// </summary>
    public class PuzzleDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string CreatedByUsername { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string PiecesDataUrl { get; set; } = string.Empty;
        public int PieceCount { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int GridColumns { get; set; }
        public int GridRows { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public int EstimatedCompletionMinutes { get; set; }
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPublic { get; set; }
        public bool IsFeatured { get; set; }
        public int TotalSessions { get; set; }
        public int TotalCompletions { get; set; }
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
    }
    
    /// <summary>
    /// Data Transfer Object for updating a puzzle
    /// </summary>
    public class UpdatePuzzleRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
        
        [MaxLength(500)]
        public string? Tags { get; set; }
        
        public bool? IsPublic { get; set; }
    }
}
