namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// User statistics entity
    /// </summary>
    public class UserStats
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int PuzzlesCompleted { get; set; }
        public int PuzzlesStarted { get; set; }
        public int TotalPiecesPlaced { get; set; }
        public TimeSpan TotalPlayTime { get; set; }
        public decimal AverageCompletionTime { get; set; }
        public int SessionsJoined { get; set; }
        public DateTime LastActiveAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public virtual User User { get; set; } = null!;
    }
}