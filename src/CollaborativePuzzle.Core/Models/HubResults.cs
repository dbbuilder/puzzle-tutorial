namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Result for leave session operation
    /// </summary>
    public class LeaveSessionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Result for unlock piece operation
    /// </summary>
    public class UnlockPieceResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Result for send chat operation
    /// </summary>
    public class SendChatResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}