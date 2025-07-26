namespace CollaborativePuzzle.Core.Entities
{
    /// <summary>
    /// Represents a category for organizing puzzles
    /// </summary>
    public class PuzzleCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public virtual ICollection<Puzzle> Puzzles { get; set; } = new HashSet<Puzzle>();
    }
}