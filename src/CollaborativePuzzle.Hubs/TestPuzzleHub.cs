using System;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Hubs
{
    /// <summary>
    /// Test version of PuzzleHub that works without authentication
    /// </summary>
    public class TestPuzzleHub : Hub
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IPieceRepository _pieceRepository;
        private readonly IRedisService _redisService;
        private readonly ILogger<TestPuzzleHub> _logger;
        
        // Store connection to user mapping for testing
        private static readonly ConcurrentDictionary<string, Guid> _connectionUsers = new();
        private static readonly ConcurrentDictionary<string, Channel<CursorUpdateNotification>> _cursorChannels = new();

        public TestPuzzleHub(
            ISessionRepository sessionRepository,
            IPieceRepository pieceRepository,
            IRedisService redisService,
            ILogger<TestPuzzleHub> logger)
        {
            _sessionRepository = sessionRepository;
            _pieceRepository = pieceRepository;
            _redisService = redisService;
            _logger = logger;
        }

        private Guid GetOrCreateUserId()
        {
            if (!_connectionUsers.TryGetValue(Context.ConnectionId, out var userId))
            {
                userId = Guid.NewGuid();
                _connectionUsers[Context.ConnectionId] = userId;
                _logger.LogInformation("Created test user {UserId} for connection {ConnectionId}", userId, Context.ConnectionId);
            }
            return userId;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetOrCreateUserId();
            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connectionUsers.TryRemove(Context.ConnectionId, out var userId))
            {
                _logger.LogInformation("User {UserId} disconnected", userId);
                
                // Clean up any session data
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                if (!string.IsNullOrEmpty(sessionId) && Guid.TryParse(sessionId, out var sessionGuid))
                {
                    await _sessionRepository.RemoveParticipantAsync(sessionGuid, userId);
                    await _pieceRepository.UnlockAllPiecesForUserAsync(userId);
                    
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserLeft", 
                        new UserLeftNotification { UserId = userId.ToString() });
                }
                
                await _redisService.DeleteAsync($"connection:{Context.ConnectionId}:session");
            }
            
            // Clean up cursor channel
            if (_cursorChannels.TryRemove(Context.ConnectionId, out var channel))
            {
                channel.Writer.TryComplete();
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<JoinSessionResult> JoinPuzzleSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return new JoinSessionResult { Success = false, Error = "Session ID is required" };
                }

                var userId = GetOrCreateUserId();
                
                // For testing, create a mock session if it doesn't exist
                var session = await _sessionRepository.GetSessionAsync(Guid.Parse(sessionId));
                if (session == null)
                {
                    _logger.LogWarning("Session {SessionId} not found, creating mock session for testing", sessionId);
                    // Return success anyway for testing
                }
                
                // Add to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"puzzle-{sessionId}");
                
                // Track connection
                await _redisService.SetStringAsync($"connection:{Context.ConnectionId}:session", sessionId, TimeSpan.FromMinutes(30));
                
                // Notify others
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserJoined", 
                    new UserJoinedNotification
                    {
                        UserId = userId.ToString(),
                        DisplayName = $"User-{userId.ToString().Substring(0, 8)}",
                        JoinedAt = DateTime.UtcNow
                    });
                
                _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
                
                return new JoinSessionResult
                {
                    Success = true,
                    SessionId = sessionId,
                    UserId = userId,
                    SessionState = new SessionStateDto
                    {
                        SessionId = sessionId,
                        PuzzleId = Guid.NewGuid().ToString(),
                        Name = "Test Session",
                        CompletionPercentage = 0,
                        Participants = new[] { new ParticipantDto { UserId = userId.ToString(), DisplayName = $"User-{userId.ToString().Substring(0, 8)}", IsOnline = true, Role = "Player" } },
                        Pieces = Array.Empty<PieceStateDto>()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
                return new JoinSessionResult { Success = false, Error = "Failed to join session" };
            }
        }

        public async Task<LeaveSessionResult> LeavePuzzleSession(string sessionId)
        {
            try
            {
                var userId = GetOrCreateUserId();
                
                // Remove from group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"puzzle-{sessionId}");
                
                // Clear tracking
                await _redisService.DeleteAsync($"connection:{Context.ConnectionId}:session");
                
                // Notify others
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserLeft", 
                    new UserLeftNotification { UserId = userId.ToString() });
                
                return new LeaveSessionResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving session {SessionId}", sessionId);
                return new LeaveSessionResult { Success = false, Error = "Failed to leave session" };
            }
        }

        public async Task<MovePieceResult> MovePiece(string pieceId, double x, double y, int rotation)
        {
            try
            {
                var userId = GetOrCreateUserId();
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    return new MovePieceResult { Success = false, Error = "Not in a session" };
                }
                
                // For testing, just broadcast the move
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceMoved",
                    new PieceMovedNotification
                    {
                        PieceId = pieceId,
                        MovedByUserId = userId.ToString(),
                        X = x,
                        Y = y,
                        Rotation = rotation,
                        MovedAt = DateTime.UtcNow
                    });
                
                return new MovePieceResult
                {
                    Success = true,
                    IsPlaced = false,
                    CompletionPercentage = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving piece {PieceId}", pieceId);
                return new MovePieceResult { Success = false, Error = "Failed to move piece" };
            }
        }

        public async Task<LockPieceResult> LockPiece(string pieceId)
        {
            try
            {
                var userId = GetOrCreateUserId();
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    return new LockPieceResult { Success = false, Error = "Not in a session" };
                }
                
                // Try to acquire lock
                var lockKey = $"piece-lock:{pieceId}";
                var lockAcquired = await _redisService.SetAsync(lockKey, userId.ToString(), TimeSpan.FromSeconds(30), When.NotExists);
                
                if (lockAcquired)
                {
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceLocked",
                        new PieceLockedNotification
                        {
                            PieceId = pieceId,
                            LockedByUserId = userId.ToString(),
                            LockExpiry = DateTime.UtcNow.AddSeconds(30)
                        });
                    
                    return new LockPieceResult { Success = true };
                }
                
                return new LockPieceResult { Success = false, Error = "Piece is already locked" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking piece {PieceId}", pieceId);
                return new LockPieceResult { Success = false, Error = "Failed to lock piece" };
            }
        }

        public async Task<UnlockPieceResult> UnlockPiece(string pieceId)
        {
            try
            {
                var userId = GetOrCreateUserId();
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    return new UnlockPieceResult { Success = false, Error = "Not in a session" };
                }
                
                // Check if user owns the lock
                var lockKey = $"piece-lock:{pieceId}";
                var lockOwner = await _redisService.GetStringAsync(lockKey);
                
                if (lockOwner == userId.ToString())
                {
                    await _redisService.DeleteAsync(lockKey);
                    
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceUnlocked",
                        new PieceUnlockedNotification
                        {
                            PieceId = pieceId,
                            UnlockedByUserId = userId.ToString()
                        });
                    
                    return new UnlockPieceResult { Success = true };
                }
                
                return new UnlockPieceResult { Success = false, Error = "You don't own this lock" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking piece {PieceId}", pieceId);
                return new UnlockPieceResult { Success = false, Error = "Failed to unlock piece" };
            }
        }

        public async Task<SendChatResult> SendChatMessage(string sessionId, string message)
        {
            try
            {
                var userId = GetOrCreateUserId();
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    return new SendChatResult { Success = false, Error = "Message cannot be empty" };
                }
                
                await Clients.Group($"puzzle-{sessionId}").SendAsync("ChatMessage",
                    new ChatMessageNotification
                    {
                        UserId = userId.ToString(),
                        SenderName = $"User-{userId.ToString().Substring(0, 8)}",
                        Message = message,
                        SentAt = DateTime.UtcNow,
                        MessageType = MessageType.User
                    });
                
                return new SendChatResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message");
                return new SendChatResult { Success = false, Error = "Failed to send message" };
            }
        }

        public async Task UpdateCursor(double x, double y)
        {
            try
            {
                var userId = GetOrCreateUserId();
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                
                if (!string.IsNullOrEmpty(sessionId))
                {
                    // Simple cursor update without throttling for testing
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("CursorUpdate",
                        new CursorUpdateNotification
                        {
                            UserId = userId.ToString(),
                            X = x,
                            Y = y
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cursor");
            }
        }
    }
}