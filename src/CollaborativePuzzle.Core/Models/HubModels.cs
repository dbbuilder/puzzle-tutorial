using System;

namespace CollaborativePuzzle.Core.Models
{
    /// <summary>
    /// Base class for all hub method results.
    /// </summary>
    public class HubResult
    {
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static T CreateSuccess<T>() where T : HubResult, new()
        {
            return new T { Success = true };
        }
        
        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static T CreateError<T>(string error) where T : HubResult, new()
        {
            return new T { Success = false, Error = error };
        }
    }
    
    /// <summary>
    /// Result of joining a puzzle session.
    /// </summary>
    public class JoinSessionResult : HubResult
    {
        /// <summary>
        /// Gets or sets the session ID that was joined.
        /// </summary>
        public string? SessionId { get; set; }
        
        /// <summary>
        /// Gets or sets the current session state.
        /// </summary>
        public SessionStateDto? SessionState { get; set; }
    }
    
    /// <summary>
    /// Result of moving a puzzle piece.
    /// </summary>
    public class MovePieceResult : HubResult
    {
        /// <summary>
        /// Gets or sets the piece ID that was moved.
        /// </summary>
        public string? PieceId { get; set; }
        
        /// <summary>
        /// Gets or sets the new position of the piece.
        /// </summary>
        public PiecePosition? NewPosition { get; set; }
        
        /// <summary>
        /// Gets or sets whether the piece is now correctly placed.
        /// </summary>
        public bool IsPlaced { get; set; }
    }
    
    /// <summary>
    /// Result of locking a puzzle piece.
    /// </summary>
    public class LockPieceResult : HubResult
    {
        /// <summary>
        /// Gets or sets the piece ID that was locked.
        /// </summary>
        public string? PieceId { get; set; }
        
        /// <summary>
        /// Gets or sets the user ID who locked the piece.
        /// </summary>
        public string? LockedBy { get; set; }
        
        /// <summary>
        /// Gets or sets when the lock expires.
        /// </summary>
        public DateTime? LockExpiry { get; set; }
    }
    
    /// <summary>
    /// Represents a piece position.
    /// </summary>
    public class PiecePosition
    {
        /// <summary>
        /// Gets or sets the X coordinate.
        /// </summary>
        public double X { get; set; }
        
        /// <summary>
        /// Gets or sets the Y coordinate.
        /// </summary>
        public double Y { get; set; }
        
        /// <summary>
        /// Gets or sets the rotation angle in degrees.
        /// </summary>
        public int Rotation { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a user joins a session.
    /// </summary>
    public class UserJoinedNotification
    {
        /// <summary>
        /// Gets or sets the user ID who joined.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user's display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets when the user joined.
        /// </summary>
        public DateTime JoinedAt { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a user leaves a session.
    /// </summary>
    public class UserLeftNotification
    {
        /// <summary>
        /// Gets or sets the user ID who left.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets when the user left.
        /// </summary>
        public DateTime LeftAt { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a piece is moved.
    /// </summary>
    public class PieceMovedNotification
    {
        /// <summary>
        /// Gets or sets the piece ID that was moved.
        /// </summary>
        public string PieceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the new X coordinate.
        /// </summary>
        public double X { get; set; }
        
        /// <summary>
        /// Gets or sets the new Y coordinate.
        /// </summary>
        public double Y { get; set; }
        
        /// <summary>
        /// Gets or sets the rotation angle.
        /// </summary>
        public int Rotation { get; set; }
        
        /// <summary>
        /// Gets or sets the user who moved the piece.
        /// </summary>
        public string MovedByUserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the piece is now correctly placed.
        /// </summary>
        public bool IsPlaced { get; set; }
        
        /// <summary>
        /// Gets or sets when the piece was moved.
        /// </summary>
        public DateTime MovedAt { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a piece is locked.
    /// </summary>
    public class PieceLockedNotification
    {
        /// <summary>
        /// Gets or sets the piece ID that was locked.
        /// </summary>
        public string PieceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user who locked the piece.
        /// </summary>
        public string LockedByUserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets when the lock expires.
        /// </summary>
        public DateTime LockExpiry { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a piece is unlocked.
    /// </summary>
    public class PieceUnlockedNotification
    {
        /// <summary>
        /// Gets or sets the piece ID that was unlocked.
        /// </summary>
        public string PieceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user who unlocked the piece.
        /// </summary>
        public string UnlockedByUserId { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Notification sent when a chat message is received.
    /// </summary>
    public class ChatMessageNotification
    {
        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public string MessageId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user who sent the message.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user's display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets when the message was sent.
        /// </summary>
        public DateTime SentAt { get; set; }
    }
    
    /// <summary>
    /// Notification sent when cursor position updates.
    /// </summary>
    public class CursorUpdateNotification
    {
        /// <summary>
        /// Gets or sets the user whose cursor moved.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the cursor X position.
        /// </summary>
        public double X { get; set; }
        
        /// <summary>
        /// Gets or sets the cursor Y position.
        /// </summary>
        public double Y { get; set; }
    }
    
    /// <summary>
    /// Notification sent when a puzzle is completed.
    /// </summary>
    public class PuzzleCompletedNotification
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the puzzle ID.
        /// </summary>
        public string PuzzleId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the completion time.
        /// </summary>
        public DateTime CompletedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the total time taken.
        /// </summary>
        public TimeSpan TotalTime { get; set; }
        
        /// <summary>
        /// Gets or sets the participant statistics.
        /// </summary>
        public ParticipantStats[]? ParticipantStats { get; set; }
    }
    
    /// <summary>
    /// Statistics for a puzzle participant.
    /// </summary>
    public class ParticipantStats
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of pieces placed.
        /// </summary>
        public int PiecesPlaced { get; set; }
        
        /// <summary>
        /// Gets or sets the time spent.
        /// </summary>
        public TimeSpan TimeSpent { get; set; }
    }
    
    /// <summary>
    /// Current state of a session.
    /// </summary>
    public class SessionStateDto
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the puzzle ID.
        /// </summary>
        public string PuzzleId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the session name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the current participants.
        /// </summary>
        public ParticipantDto[]? Participants { get; set; }
        
        /// <summary>
        /// Gets or sets the completion percentage.
        /// </summary>
        public decimal CompletionPercentage { get; set; }
        
        /// <summary>
        /// Gets or sets the puzzle pieces state.
        /// </summary>
        public PieceStateDto[]? Pieces { get; set; }
    }
    
    /// <summary>
    /// Participant information.
    /// </summary>
    public class ParticipantDto
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the user is online.
        /// </summary>
        public bool IsOnline { get; set; }
        
        /// <summary>
        /// Gets or sets the user role.
        /// </summary>
        public string Role { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Piece state information.
    /// </summary>
    public class PieceStateDto
    {
        /// <summary>
        /// Gets or sets the piece ID.
        /// </summary>
        public string PieceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the current position.
        /// </summary>
        public PiecePosition Position { get; set; } = new();
        
        /// <summary>
        /// Gets or sets whether the piece is locked.
        /// </summary>
        public bool IsLocked { get; set; }
        
        /// <summary>
        /// Gets or sets who locked the piece.
        /// </summary>
        public string? LockedBy { get; set; }
        
        /// <summary>
        /// Gets or sets whether the piece is correctly placed.
        /// </summary>
        public bool IsPlaced { get; set; }
    }
}