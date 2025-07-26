using System;

namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Data Transfer Object for puzzle piece information
    /// </summary>
    public class PuzzlePieceDto
    {
        public Guid Id { get; set; }
        public Guid PuzzleId { get; set; }
        public int PieceNumber { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int CorrectX { get; set; }
        public int CorrectY { get; set; }
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }
        public int Rotation { get; set; }
        public string ShapeData { get; set; } = string.Empty;
        public int ImageX { get; set; }
        public int ImageY { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public bool IsPlaced { get; set; }
        public bool IsEdgePiece { get; set; }
        public bool IsCornerPiece { get; set; }
        public Guid? LockedByUserId { get; set; }
        public string? LockedByUsername { get; set; }
        public DateTime? LockedAt { get; set; }
    }
    
    /// <summary>
    /// Data Transfer Object for moving a puzzle piece
    /// </summary>
    public class MovePieceRequest
    {
        public Guid PieceId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Rotation { get; set; }
        public bool CheckPlacement { get; set; } = true;
    }
    
    /// <summary>
    /// Data Transfer Object for locking/unlocking a puzzle piece
    /// </summary>
    public class PieceLockRequest
    {
        public Guid PieceId { get; set; }
        public bool Lock { get; set; }
    }
    
    /// <summary>
    /// Data Transfer Object for piece movement result
    /// </summary>
    public class PieceMoveResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsPlaced { get; set; }
        public bool WasAlreadyPlaced { get; set; }
        public int FinalX { get; set; }
        public int FinalY { get; set; }
        public int FinalRotation { get; set; }
        public int CompletedPieces { get; set; }
        public decimal CompletionPercentage { get; set; }
        public bool PuzzleCompleted { get; set; }
    }
}
