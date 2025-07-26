namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Represents the current state of a session (simple version)
    /// </summary>
    public class SessionState
    {
        public int ParticipantCount { get; set; }
        public int CompletedPieces { get; set; }
        public int TotalPieces { get; set; }
    }
}