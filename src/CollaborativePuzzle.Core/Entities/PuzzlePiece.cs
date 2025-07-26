using System;
using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents an individual piece of a jigsaw puzzle with position and shape data
    /// </summary>
    public class PuzzlePiece
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid PuzzleId { get; set; }
        
        public int PieceNumber { get; set; }
        
        // Grid position in the puzzle layout
        public int GridX { get; set; }
        public int GridY { get; set; }
        
        // Correct position in completed puzzle (pixel coordinates)
        public int CorrectX { get; set; }
        public int CorrectY { get; set; }
        
        // Current position in puzzle session (pixel coordinates)
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }

        // Rotation angle (0, 90, 180, 270 degrees)
        public int Rotation { get; set; }

        // Shape definition as JSON string containing SVG path data
        [Required]
        public string ShapeData { get; set; } = string.Empty;
        
        // Piece image section coordinates within the original image
        public int ImageX { get; set; }
        public int ImageY { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        // Indicates if piece is correctly placed
        public bool IsPlaced { get; set; }

        // Indicates if piece is an edge or corner piece
        public bool IsEdgePiece { get; set; } = false;
        public bool IsCornerPiece { get; set; }

        // Locking mechanism for collaborative editing
        public Guid? LockedByUserId { get; set; }
        public DateTime? LockedAt { get; set; }
        
        // Version for optimistic concurrency control
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual Puzzle Puzzle { get; set; } = null!;
        public virtual User? LockedByUser { get; set; }
    }
}
