namespace CollaborativePuzzle.Core.Enums
{
    /// <summary>
    /// Represents the difficulty level of a puzzle based on piece count and complexity
    /// </summary>
    public enum PuzzleDifficulty
    {
        /// <summary>
        /// Easy puzzles with 100-300 pieces, simple patterns
        /// </summary>
        Easy = 1,
        
        /// <summary>
        /// Medium puzzles with 300-750 pieces, moderate complexity
        /// </summary>
        Medium = 2,
        
        /// <summary>
        /// Hard puzzles with 750-1500 pieces, complex patterns
        /// </summary>
        Hard = 3,
        
        /// <summary>
        /// Expert puzzles with 1500+ pieces, very complex patterns
        /// </summary>
        Expert = 4
    }
}
