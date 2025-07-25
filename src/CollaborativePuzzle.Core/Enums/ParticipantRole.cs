namespace CollaborativePuzzle.Core.Enums
{
    /// <summary>
    /// Represents the role of a participant within a puzzle session
    /// </summary>
    public enum ParticipantRole
    {
        /// <summary>
        /// Regular participant with standard puzzle solving permissions
        /// </summary>
        Participant = 1,
        
        /// <summary>
        /// Moderator with ability to manage session settings and other participants
        /// </summary>
        Moderator = 2,
        
        /// <summary>
        /// Session owner with full administrative control over the session
        /// </summary>
        Owner = 3,
        
        /// <summary>
        /// Observer who can view but not interact with the puzzle
        /// </summary>
        Observer = 4
    }
}
