using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Core.Entities;
using System.Text.Json;

namespace CollaborativePuzzle.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time puzzle collaboration
    /// Handles piece movements, user presence, and chat messages
    /// </summary>
    [Authorize]
    public class PuzzleHub : Hub
    {
        private readonly ILogger<PuzzleHub> _logger;
        private readonly IPieceRepository _pieceRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IRedisService _redisService;
        
        // Connection tracking for session management
        private static readonly Dictionary<string, string> _connectionSessions = new();
        private static readonly Dictionary<string, Guid> _connectionUsers = new();

        public PuzzleHub(
            ILogger<PuzzleHub> logger,
            IPieceRepository pieceRepository,
            ISessionRepository sessionRepository,
            IRedisService redisService)
        {
            _logger = logger;
            _pieceRepository = pieceRepository;
            _sessionRepository = sessionRepository;
            _redisService = redisService;
        }

        #region Connection Management

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, connectionId);

                // Track connection
                _connectionUsers[connectionId] = userId;

                // Update user's last active time in cache
                await _redisService.SetStringAsync($"user:lastseen:{userId}", DateTimeOffset.UtcNow.ToString(), TimeSpan.FromHours(24));

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection establishment for {ConnectionId}", Context.ConnectionId);
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var connectionId = Context.ConnectionId;
                var userId = GetCurrentUserId();

                _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, connectionId);

                // Handle disconnection cleanup
                if (_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    await HandleUserDisconnection(sessionId, userId, connectionId);
                }

                // Clean up tracking dictionaries
                _connectionSessions.Remove(connectionId);
                _connectionUsers.Remove(connectionId);

                // Release any locked pieces by this user
                await _pieceRepository.UnlockAllPiecesByUserAsync(userId);

                if (exception != null)
                {
                    _logger.LogWarning(exception, "User {UserId} disconnected due to exception", userId);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnection handling for {ConnectionId}", Context.ConnectionId);
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Join a puzzle session for collaborative solving
        /// </summary>
        /// <param name="sessionId">Session identifier to join</param>
        public async Task JoinPuzzleSession(string sessionId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                _logger.LogInformation("User {UserId} attempting to join session {SessionId}", userId, sessionId);

                if (!Guid.TryParse(sessionId, out var sessionGuid))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid session ID format");
                    return;
                }

                // Verify session exists and is active
                var session = await _sessionRepository.GetSessionWithParticipantsAsync(sessionGuid);
                if (session == null || session.Status != Core.Enums.SessionStatus.Active)
                {
                    await Clients.Caller.SendAsync("Error", "Session not found or not active");
                    return;
                }

                // Check if session is full
                if (session.Participants.Count(p => p.Status == Core.Enums.ParticipantStatus.Online) >= session.MaxParticipants)
                {
                    await Clients.Caller.SendAsync("Error", "Session is full");
                    return;
                }

                // Join SignalR group
                await Groups.AddToGroupAsync(connectionId, $"session_{sessionId}");

                // Track connection to session
                _connectionSessions[connectionId] = sessionId;

                // Update participant status in database via stored procedure
                // This would call a stored procedure to add/update participant
                
                // Cache user's session participation
                await _redisService.SetStringAsync($"user:session:{userId}", sessionId, TimeSpan.FromHours(8));

                // Notify other users in the session
                await Clients.Group($"session_{sessionId}").SendAsync("UserJoined", new
                {
                    UserId = userId,
                    Username = GetCurrentUsername(),
                    JoinedAt = DateTimeOffset.UtcNow
                });

                // Send session state to the joining user
                await SendSessionStateToUser(sessionGuid, userId);

                _logger.LogInformation("User {UserId} successfully joined session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session {SessionId} for user {UserId}", sessionId, GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to join session");
            }
        }

        /// <summary>
        /// Leave the current puzzle session
        /// </summary>
        public async Task LeavePuzzleSession()
        {
            try
            {
                var connectionId = Context.ConnectionId;
                var userId = GetCurrentUserId();

                if (_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    _logger.LogInformation("User {UserId} leaving session {SessionId}", userId, sessionId);

                    await HandleUserDisconnection(sessionId, userId, connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving session for user {UserId}", GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to leave session");
            }
        }

        #endregion

        #region Piece Movement

        /// <summary>
        /// Move a puzzle piece to a new position
        /// </summary>
        /// <param name="pieceId">Piece identifier</param>
        /// <param name="x">New X coordinate</param>
        /// <param name="y">New Y coordinate</param>
        /// <param name="rotation">New rotation angle (0, 90, 180, 270)</param>
        public async Task MovePiece(string pieceId, int x, int y, int rotation = 0)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                if (!_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    await Clients.Caller.SendAsync("Error", "Not connected to a session");
                    return;
                }

                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid piece ID format");
                    return;
                }

                _logger.LogDebug("User {UserId} moving piece {PieceId} to ({X}, {Y}) with rotation {Rotation}", 
                    userId, pieceId, x, y, rotation);

                // Update piece position using stored procedure
                var result = await _pieceRepository.UpdatePiecePositionAsync(pieceGuid, x, y, rotation, true);

                if (result.Success)
                {
                    // Broadcast piece movement to all users in the session (except sender)
                    await Clients.GroupExcept($"session_{sessionId}", connectionId).SendAsync("PieceMoved", new
                    {
                        PieceId = pieceId,
                        X = result.FinalX,
                        Y = result.FinalY,
                        Rotation = result.FinalRotation,
                        IsPlaced = result.IsPlaced,
                        UserId = userId,
                        Username = GetCurrentUsername(),
                        Timestamp = DateTimeOffset.UtcNow
                    });

                    // Send success confirmation to sender
                    await Clients.Caller.SendAsync("PieceMoveConfirmed", new
                    {
                        PieceId = pieceId,
                        X = result.FinalX,
                        Y = result.FinalY,
                        Rotation = result.FinalRotation,
                        IsPlaced = result.IsPlaced,
                        CompletedPieces = result.CompletedPieces,
                        CompletionPercentage = result.CompletionPercentage
                    });

                    // Check for puzzle completion
                    if (result.PuzzleCompleted)
                    {
                        await Clients.Group($"session_{sessionId}").SendAsync("PuzzleCompleted", new
                        {
                            CompletedBy = userId,
                            CompletedByUsername = GetCurrentUsername(),
                            CompletedAt = DateTimeOffset.UtcNow,
                            TotalPieces = result.CompletedPieces
                        });

                        _logger.LogInformation("Puzzle completed in session {SessionId} by user {UserId}", sessionId, userId);
                    }

                    // Update progress in Redis cache
                    await _redisService.SetObjectAsync($"session:progress:{sessionId}", new
                    {
                        CompletedPieces = result.CompletedPieces,
                        CompletionPercentage = result.CompletionPercentage,
                        LastUpdated = DateTimeOffset.UtcNow
                    }, TimeSpan.FromHours(24));
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to move piece");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving piece {PieceId} for user {UserId}", pieceId, GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to move piece");
            }
        }

        /// <summary>
        /// Lock a puzzle piece for exclusive editing
        /// </summary>
        /// <param name="pieceId">Piece identifier to lock</param>
        public async Task LockPiece(string pieceId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                if (!_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    await Clients.Caller.SendAsync("Error", "Not connected to a session");
                    return;
                }

                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid piece ID format");
                    return;
                }

                var success = await _pieceRepository.LockPieceAsync(pieceGuid, userId);

                if (success)
                {
                    // Notify all users in the session about the lock
                    await Clients.Group($"session_{sessionId}").SendAsync("PieceLocked", new
                    {
                        PieceId = pieceId,
                        LockedBy = userId,
                        LockedByUsername = GetCurrentUsername(),
                        LockedAt = DateTimeOffset.UtcNow
                    });

                    _logger.LogDebug("Piece {PieceId} locked by user {UserId}", pieceId, userId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "Failed to lock piece - may already be locked");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking piece {PieceId} for user {UserId}", pieceId, GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to lock piece");
            }
        }

        /// <summary>
        /// Unlock a puzzle piece
        /// </summary>
        /// <param name="pieceId">Piece identifier to unlock</param>
        public async Task UnlockPiece(string pieceId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                if (!_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    await Clients.Caller.SendAsync("Error", "Not connected to a session");
                    return;
                }

                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid piece ID format");
                    return;
                }

                var success = await _pieceRepository.UnlockPieceAsync(pieceGuid, userId);

                if (success)
                {
                    // Notify all users in the session about the unlock
                    await Clients.Group($"session_{sessionId}").SendAsync("PieceUnlocked", new
                    {
                        PieceId = pieceId,
                        UnlockedBy = userId,
                        UnlockedByUsername = GetCurrentUsername(),
                        UnlockedAt = DateTimeOffset.UtcNow
                    });

                    _logger.LogDebug("Piece {PieceId} unlocked by user {UserId}", pieceId, userId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "Failed to unlock piece - you may not own the lock");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking piece {PieceId} for user {UserId}", pieceId, GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to unlock piece");
            }
        }

        #endregion

        #region Chat and Communication

        /// <summary>
        /// Send a chat message to the session
        /// </summary>
        /// <param name="message">Message content</param>
        public async Task SendChatMessage(string message)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                if (!_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    await Clients.Caller.SendAsync("Error", "Not connected to a session");
                    return;
                }

                if (string.IsNullOrWhiteSpace(message) || message.Length > 1000)
                {
                    await Clients.Caller.SendAsync("Error", "Invalid message length");
                    return;
                }

                // Store message in database (would call stored procedure)
                var chatMessage = new
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    Username = GetCurrentUsername(),
                    Message = message.Trim(),
                    Type = "Text",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // Broadcast message to all users in the session
                await Clients.Group($"session_{sessionId}").SendAsync("ChatMessage", chatMessage);

                _logger.LogDebug("Chat message sent by user {UserId} in session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message for user {UserId}", GetCurrentUserId());
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        /// <summary>
        /// Update user's cursor position for collaborative indicators
        /// </summary>
        /// <param name="x">Cursor X coordinate</param>
        /// <param name="y">Cursor Y coordinate</param>
        public async Task UpdateCursor(int x, int y)
        {
            try
            {
                var userId = GetCurrentUserId();
                var connectionId = Context.ConnectionId;

                if (!_connectionSessions.TryGetValue(connectionId, out var sessionId))
                {
                    return; // Silently ignore if not in session
                }

                // Broadcast cursor position to other users (high frequency, so exclude sender)
                await Clients.GroupExcept($"session_{sessionId}", connectionId).SendAsync("CursorUpdate", new
                {
                    UserId = userId,
                    X = x,
                    Y = y,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cursor for user {UserId}", GetCurrentUserId());
                // Don't send error for cursor updates to avoid spam
            }
        }

        #endregion

        #region Helper Methods

        private Guid GetCurrentUserId()
        {
            var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdString, out var userId) ? userId : Guid.Empty;
        }

        private string GetCurrentUsername()
        {
            return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }

        private async Task HandleUserDisconnection(string sessionId, Guid userId, string connectionId)
        {
            try
            {
                // Remove from SignalR group
                await Groups.RemoveFromGroupAsync(connectionId, $"session_{sessionId}");

                // Release any locked pieces
                await _pieceRepository.UnlockAllPiecesByUserAsync(userId);

                // Update participant status (would call stored procedure)
                
                // Notify other users
                await Clients.Group($"session_{sessionId}").SendAsync("UserLeft", new
                {
                    UserId = userId,
                    Username = GetCurrentUsername(),
                    LeftAt = DateTimeOffset.UtcNow
                });

                // Clean up session tracking
                _connectionSessions.Remove(connectionId);

                _logger.LogInformation("User {UserId} disconnection handled for session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user disconnection for user {UserId} in session {SessionId}", userId, sessionId);
            }
        }

        private async Task SendSessionStateToUser(Guid sessionId, Guid userId)
        {
            try
            {
                // Get current session state from cache or database
                var sessionState = await _redisService.GetObjectAsync<object>($"session:state:{sessionId}");

                if (sessionState != null)
                {
                    await Clients.User(userId.ToString()).SendAsync("SessionState", sessionState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending session state to user {UserId} for session {SessionId}", userId, sessionId);
            }
        }

        #endregion
    }
}
