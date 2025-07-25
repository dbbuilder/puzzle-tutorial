namespace CollaborativePuzzle.Core.Enums
{
    /// <summary>
    /// Represents the current connection status of a session participant
    /// </summary>
    public enum ParticipantStatus
    {
        /// <summary>
        /// Participant is actively connected and engaged with the puzzle
        /// </summary>
        Online = 1,
        
        /// <summary>
        /// Participant is connected but inactive for a period of time
        /// </summary>
        Away = 2,
        
        /// <summary>
        /// Participant is temporarily disconnected but session is maintained
        /// </summary>
        Disconnected = 3,
        
        /// <summary>
        /// Participant has permanently left the session
        /// </summary>
        Left = 4
    }
}
