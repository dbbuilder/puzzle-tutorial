namespace CollaborativePuzzle.Core.Enums
{
    /// <summary>
    /// Represents the type of chat message within a puzzle session
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Standard text message from a user
        /// </summary>
        Text = 1,
        
        /// <summary>
        /// System-generated message about session events
        /// </summary>
        System = 2,
        
        /// <summary>
        /// Announcement message from session moderators
        /// </summary>
        Announcement = 3,
        
        /// <summary>
        /// Automated hint or tip message
        /// </summary>
        Hint = 4,
        
        /// <summary>
        /// Message celebrating puzzle milestones or completion
        /// </summary>
        Celebration = 5
    }
}
