namespace CollaborativePuzzle.Core.Enums
{
    /// <summary>
    /// Represents the current status of a collaborative puzzle session
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// Default/unknown status
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Session is active and accepting participants
        /// </summary>
        Active = 1,
        
        /// <summary>
        /// Session is in progress (alias for Active)
        /// </summary>
        InProgress = 1,
        
        /// <summary>
        /// Session is paused, participants can rejoin but no new participants allowed
        /// </summary>
        Paused = 2,
        
        /// <summary>
        /// Session is completed, puzzle has been solved
        /// </summary>
        Completed = 3,
        
        /// <summary>
        /// Session has been cancelled by the owner
        /// </summary>
        Cancelled = 4,
        
        /// <summary>
        /// Session has been automatically closed due to inactivity
        /// </summary>
        Expired = 5
    }
}
