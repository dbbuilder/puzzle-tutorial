using System;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Core.Enums;

namespace CollaborativePuzzle.Hubs
{
    /// <summary>
    /// SignalR hub for real-time puzzle collaboration with Redis backplane support.
    /// Handles piece movements, locking, chat, and session management.
    /// </summary>
    // [Authorize] // Temporarily disabled for testing
    public class PuzzleHub : Hub
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IPieceRepository _pieceRepository;
        private readonly IRedisService _redisService;
        private readonly ILogger<PuzzleHub> _logger;
        
        // Throttling for cursor updates - limit to 10 updates per second per user
        private static readonly ConcurrentDictionary<string, Channel<CursorUpdateNotification>> _cursorChannels = new();
        private static readonly TimeSpan CursorThrottleInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan PieceLockDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ConnectionTrackingExpiry = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="PuzzleHub"/> class.
        /// </summary>
        public PuzzleHub(
            ISessionRepository sessionRepository,
            IPieceRepository pieceRepository,
            IRedisService redisService,
            ILogger<PuzzleHub> logger)
        {
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _pieceRepository = pieceRepository ?? throw new ArgumentNullException(nameof(pieceRepository));
            _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a new connection is established.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", 
                Context.UserIdentifier, Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a connection is terminated.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", 
                Context.UserIdentifier, Context.ConnectionId);
            
            try
            {
                // Get session info from Redis
                var sessionId = await _redisService.GetStringAsync($"connection:{Context.ConnectionId}:session");
                if (!string.IsNullOrEmpty(sessionId) && Guid.TryParse(sessionId, out var sessionGuid))
                {
                    var userId = Guid.Parse(Context.UserIdentifier!);
                    
                    // Remove from session
                    await _sessionRepository.RemoveParticipantAsync(sessionGuid, userId);
                    
                    // Unlock all pieces held by this user
                    await _pieceRepository.UnlockAllPiecesForUserAsync(userId);
                    
                    // Notify others in the session
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserLeft", 
                        new UserLeftNotification
                        {
                            UserId = Context.UserIdentifier!,
                            SessionId = sessionId,
                            LeftAt = DateTime.UtcNow
                        });
                }
                
                // Clean up Redis tracking
                await _redisService.DeleteAsync($"connection:{Context.ConnectionId}");
                await _redisService.DeleteAsync($"user:{Context.UserIdentifier}:session");
                
                // Clean up cursor channel
                if (_cursorChannels.TryRemove(Context.ConnectionId, out var channel))
                {
                    channel.Writer.TryComplete();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnect cleanup for user {UserId}", Context.UserIdentifier);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Joins a puzzle session.
        /// </summary>
        /// <param name="sessionId">The session ID to join.</param>
        /// <returns>The join result with session state.</returns>
        public async Task<JoinSessionResult> JoinPuzzleSession(string sessionId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var sessionGuid))
                {
                    return HubResult.CreateError<JoinSessionResult>("Invalid session ID format");
                }
                
                var userId = Guid.Parse(Context.UserIdentifier!);
                
                // Verify session exists and is active
                var session = await _sessionRepository.GetSessionAsync(sessionGuid);
                if (session == null)
                {
                    return HubResult.CreateError<JoinSessionResult>("Session not found");
                }
                
                if (session.Status != SessionStatus.InProgress)
                {
                    return HubResult.CreateError<JoinSessionResult>("Session is not active");
                }
                
                // Add participant to session
                var participant = await _sessionRepository.AddParticipantAsync(sessionGuid, userId, Context.ConnectionId);
                
                // Add to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"puzzle-{sessionId}");
                
                // Track connection in Redis
                await _redisService.SetAsync($"connection:{Context.ConnectionId}", 
                    new { SessionId = sessionId, UserId = userId }, 
                    ConnectionTrackingExpiry);
                await _redisService.SetAsync($"connection:{Context.ConnectionId}:session", sessionId, ConnectionTrackingExpiry);
                await _redisService.SetAsync($"user:{userId}:session", sessionId, ConnectionTrackingExpiry);
                
                // Notify others in the session
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("UserJoined", 
                    new UserJoinedNotification
                    {
                        UserId = userId.ToString(),
                        DisplayName = participant.User?.DisplayName ?? "Unknown",
                        SessionId = sessionId,
                        JoinedAt = DateTime.UtcNow
                    });
                
                // Get current session state
                var sessionState = await GetSessionState(sessionGuid);
                
                _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
                
                return new JoinSessionResult
                {
                    Success = true,
                    SessionId = sessionId,
                    SessionState = sessionState
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
                return HubResult.CreateError<JoinSessionResult>("Failed to join session");
            }
        }

        /// <summary>
        /// Moves a puzzle piece.
        /// </summary>
        /// <param name="pieceId">The piece ID to move.</param>
        /// <param name="x">The new X coordinate.</param>
        /// <param name="y">The new Y coordinate.</param>
        /// <param name="rotation">The rotation angle in degrees.</param>
        /// <returns>The move result.</returns>
        public async Task<MovePieceResult> MovePiece(string pieceId, double x, double y, int rotation)
        {
            try
            {
                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    return HubResult.CreateError<MovePieceResult>("Invalid piece ID format");
                }
                
                var userId = Guid.Parse(Context.UserIdentifier!);
                
                // Verify user is in a session
                var sessionId = await _redisService.GetStringAsync($"user:{userId}:session");
                if (string.IsNullOrEmpty(sessionId))
                {
                    return HubResult.CreateError<MovePieceResult>("User not in a session");
                }
                
                // Get the piece
                var piece = await _pieceRepository.GetPieceAsync(pieceGuid);
                if (piece == null)
                {
                    return HubResult.CreateError<MovePieceResult>("Piece not found");
                }
                
                // Check if piece is locked by another user
                if (piece.LockedByUserId.HasValue && piece.LockedByUserId.Value != userId)
                {
                    return HubResult.CreateError<MovePieceResult>("Piece is locked by another user");
                }
                
                // Update piece position
                var updated = await _pieceRepository.UpdatePiecePositionAsync(pieceGuid, x, y, rotation);
                if (!updated)
                {
                    return HubResult.CreateError<MovePieceResult>("Failed to update piece position");
                }
                
                // Check if piece is now correctly placed
                var isPlaced = CheckIfPiecePlaced(piece, x, y, rotation);
                if (isPlaced && !piece.IsPlaced)
                {
                    await _pieceRepository.MarkPieceAsPlacedAsync(pieceGuid);
                }
                
                // Notify others in the group
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceMoved", 
                    new PieceMovedNotification
                    {
                        PieceId = pieceId,
                        X = x,
                        Y = y,
                        Rotation = rotation,
                        MovedByUserId = userId.ToString(),
                        IsPlaced = isPlaced,
                        MovedAt = DateTime.UtcNow
                    });
                
                _logger.LogDebug("User {UserId} moved piece {PieceId} to ({X}, {Y})", userId, pieceId, x, y);
                
                return new MovePieceResult
                {
                    Success = true,
                    PieceId = pieceId,
                    NewPosition = new PiecePosition { X = x, Y = y, Rotation = rotation },
                    IsPlaced = isPlaced
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving piece {PieceId}", pieceId);
                return HubResult.CreateError<MovePieceResult>("Failed to move piece");
            }
        }

        /// <summary>
        /// Locks a puzzle piece for exclusive editing.
        /// </summary>
        /// <param name="pieceId">The piece ID to lock.</param>
        /// <returns>The lock result.</returns>
        public async Task<LockPieceResult> LockPiece(string pieceId)
        {
            try
            {
                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    return HubResult.CreateError<LockPieceResult>("Invalid piece ID format");
                }
                
                var userId = Guid.Parse(Context.UserIdentifier!);
                
                // Get the piece
                var piece = await _pieceRepository.GetPieceAsync(pieceGuid);
                if (piece == null)
                {
                    return HubResult.CreateError<LockPieceResult>("Piece not found");
                }
                
                // Try to acquire distributed lock via Redis
                var lockAcquired = await _redisService.SetAsync(
                    $"piece-lock:{pieceId}", 
                    userId.ToString(), 
                    PieceLockDuration, 
                    When.NotExists);
                
                if (!lockAcquired)
                {
                    // Check who has the lock
                    var currentLockHolder = await _redisService.GetStringAsync($"piece-lock:{pieceId}");
                    return new LockPieceResult
                    {
                        Success = false,
                        Error = "Piece is already locked",
                        PieceId = pieceId,
                        LockedBy = currentLockHolder ?? piece.LockedByUserId?.ToString()
                    };
                }
                
                // Update database
                var dbLocked = await _pieceRepository.LockPieceAsync(pieceGuid, userId);
                if (!dbLocked)
                {
                    // Release Redis lock if DB update failed
                    await _redisService.DeleteAsync($"piece-lock:{pieceId}");
                    return HubResult.CreateError<LockPieceResult>("Failed to lock piece in database");
                }
                
                var sessionId = await _redisService.GetStringAsync($"user:{userId}:session");
                var lockExpiry = DateTime.UtcNow.Add(PieceLockDuration);
                
                // Notify others
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceLocked", 
                    new PieceLockedNotification
                    {
                        PieceId = pieceId,
                        LockedByUserId = userId.ToString(),
                        LockExpiry = lockExpiry
                    });
                
                _logger.LogDebug("User {UserId} locked piece {PieceId}", userId, pieceId);
                
                return new LockPieceResult
                {
                    Success = true,
                    PieceId = pieceId,
                    LockedBy = userId.ToString(),
                    LockExpiry = lockExpiry
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking piece {PieceId}", pieceId);
                return HubResult.CreateError<LockPieceResult>("Failed to lock piece");
            }
        }

        /// <summary>
        /// Unlocks a puzzle piece.
        /// </summary>
        /// <param name="pieceId">The piece ID to unlock.</param>
        /// <returns>Success status.</returns>
        public async Task<bool> UnlockPiece(string pieceId)
        {
            try
            {
                if (!Guid.TryParse(pieceId, out var pieceGuid))
                {
                    return false;
                }
                
                var userId = Guid.Parse(Context.UserIdentifier!);
                
                // Verify user owns the lock
                var lockHolder = await _redisService.GetStringAsync($"piece-lock:{pieceId}");
                if (lockHolder != userId.ToString())
                {
                    return false;
                }
                
                // Release distributed lock
                await _redisService.DeleteAsync($"piece-lock:{pieceId}");
                
                // Update database
                await _pieceRepository.UnlockPieceAsync(pieceGuid);
                
                var sessionId = await _redisService.GetStringAsync($"user:{userId}:session");
                
                // Notify others
                await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("PieceUnlocked", 
                    new PieceUnlockedNotification
                    {
                        PieceId = pieceId,
                        UnlockedByUserId = userId.ToString()
                    });
                
                _logger.LogDebug("User {UserId} unlocked piece {PieceId}", userId, pieceId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking piece {PieceId}", pieceId);
                return false;
            }
        }

        /// <summary>
        /// Sends a chat message to the session.
        /// </summary>
        /// <param name="message">The message content.</param>
        public async Task SendChatMessage(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
                
                var userId = Guid.Parse(Context.UserIdentifier!);
                var sessionId = await _redisService.GetStringAsync($"user:{userId}:session");
                
                if (string.IsNullOrEmpty(sessionId) || !Guid.TryParse(sessionId, out var sessionGuid))
                {
                    return;
                }
                
                // Save message to database
                var chatMessage = await _sessionRepository.SaveChatMessageAsync(
                    sessionGuid, userId, message, MessageType.Chat);
                
                // Get user info for display name
                var participant = await _sessionRepository.GetParticipantAsync(sessionGuid, userId);
                
                // Broadcast to all in session (including sender)
                await Clients.Group($"puzzle-{sessionId}").SendAsync("ChatMessage", 
                    new ChatMessageNotification
                    {
                        MessageId = chatMessage.Id.ToString(),
                        Message = message,
                        UserId = userId.ToString(),
                        DisplayName = participant?.User?.DisplayName ?? "Unknown",
                        SessionId = sessionId,
                        SentAt = chatMessage.CreatedAt
                    });
                
                _logger.LogDebug("User {UserId} sent chat message in session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message");
            }
        }

        /// <summary>
        /// Updates the user's cursor position (throttled).
        /// </summary>
        /// <param name="x">The cursor X position.</param>
        /// <param name="y">The cursor Y position.</param>
        public async Task UpdateCursor(double x, double y)
        {
            try
            {
                var userId = Context.UserIdentifier!;
                var sessionId = await _redisService.GetStringAsync($"user:{userId}:session");
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    return;
                }
                
                // Get or create cursor channel for throttling
                var capturedSessionId = sessionId; // Capture sessionId
                var channel = _cursorChannels.GetOrAdd(Context.ConnectionId, connectionId =>
                {
                    var ch = Channel.CreateUnbounded<CursorUpdateNotification>();
                    Task.Run(async () => await ProcessCursorUpdates(ch.Reader, capturedSessionId));
                    return ch;
                });
                
                // Queue cursor update
                await channel.Writer.WriteAsync(new CursorUpdateNotification
                {
                    UserId = userId,
                    X = x,
                    Y = y
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cursor position");
            }
        }

        /// <summary>
        /// Checks if the puzzle is completed.
        /// </summary>
        /// <param name="sessionId">The session ID to check.</param>
        public async Task CheckPuzzleCompletion(string sessionId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var sessionGuid))
                {
                    return;
                }
                
                var session = await _sessionRepository.GetSessionAsync(sessionGuid);
                if (session == null || session.Status != SessionStatus.InProgress)
                {
                    return;
                }
                
                // Check placed piece count
                var puzzleId = session.PuzzleId;
                var placedCount = await _pieceRepository.GetPlacedPieceCountAsync(puzzleId);
                var totalCount = await _pieceRepository.GetTotalPieceCountAsync(puzzleId);
                
                if (placedCount >= totalCount)
                {
                    // Puzzle completed!
                    await _sessionRepository.CompleteSessionAsync(sessionGuid);
                    
                    // Get participant stats
                    var participants = await _sessionRepository.GetSessionParticipantsAsync(sessionGuid);
                    var stats = participants.Select(p => new ParticipantStats
                    {
                        UserId = p.UserId.ToString(),
                        DisplayName = p.User?.DisplayName ?? "Unknown",
                        PiecesPlaced = p.PiecesPlaced,
                        TimeSpent = p.TotalActiveTime
                    }).ToArray();
                    
                    // Notify all participants
                    await Clients.Group($"puzzle-{sessionId}").SendAsync("PuzzleCompleted", 
                        new PuzzleCompletedNotification
                        {
                            SessionId = sessionId,
                            PuzzleId = session.PuzzleId.ToString(),
                            CompletedAt = DateTime.UtcNow,
                            TotalTime = DateTime.UtcNow - session.CreatedAt,
                            ParticipantStats = stats
                        });
                    
                    _logger.LogInformation("Puzzle completed in session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking puzzle completion");
            }
        }

        /// <summary>
        /// Processes throttled cursor updates.
        /// </summary>
        private async Task ProcessCursorUpdates(ChannelReader<CursorUpdateNotification> reader, string sessionId)
        {
            var lastUpdate = DateTime.UtcNow;
            CursorUpdateNotification? latestUpdate = null;
            
            try
            {
                await foreach (var update in reader.ReadAllAsync())
                {
                    latestUpdate = update;
                    
                    // Check if enough time has passed
                    if (DateTime.UtcNow - lastUpdate >= CursorThrottleInterval)
                    {
                        // Publish to Redis for other servers
                        await _redisService.PublishAsync($"cursor:{sessionId}", latestUpdate);
                        
                        // Send to others in group
                        await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("CursorUpdate", latestUpdate);
                        
                        lastUpdate = DateTime.UtcNow;
                        latestUpdate = null;
                    }
                }
                
                // Send final update if any
                if (latestUpdate != null)
                {
                    await _redisService.PublishAsync($"cursor:{sessionId}", latestUpdate);
                    await Clients.OthersInGroup($"puzzle-{sessionId}").SendAsync("CursorUpdate", latestUpdate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cursor updates");
            }
        }

        /// <summary>
        /// Gets the current session state.
        /// </summary>
        private async Task<SessionStateDto> GetSessionState(Guid sessionId)
        {
            var session = await _sessionRepository.GetSessionAsync(sessionId);
            var participants = await _sessionRepository.GetSessionParticipantsAsync(sessionId);
            var pieces = await _pieceRepository.GetPuzzlePiecesAsync(session!.PuzzleId);
            
            return new SessionStateDto
            {
                SessionId = sessionId.ToString(),
                PuzzleId = session.PuzzleId.ToString(),
                Name = session.Name,
                CompletionPercentage = session.CompletionPercentage,
                Participants = participants.Select(p => new ParticipantDto
                {
                    UserId = p.UserId.ToString(),
                    DisplayName = p.User?.DisplayName ?? "Unknown",
                    IsOnline = p.Status == ParticipantStatus.Active,
                    Role = p.Role.ToString()
                }).ToArray(),
                Pieces = pieces.Select(p => new PieceStateDto
                {
                    PieceId = p.Id.ToString(),
                    Position = new PiecePosition 
                    { 
                        X = p.CurrentX, 
                        Y = p.CurrentY, 
                        Rotation = p.Rotation 
                    },
                    IsLocked = p.LockedByUserId.HasValue,
                    LockedBy = p.LockedByUserId?.ToString(),
                    IsPlaced = p.IsPlaced
                }).ToArray()
            };
        }

        /// <summary>
        /// Checks if a piece is correctly placed within tolerance.
        /// </summary>
        private bool CheckIfPiecePlaced(Core.Entities.PuzzlePiece piece, double x, double y, int rotation)
        {
            const double PositionTolerance = 5.0; // 5 pixels
            const int RotationTolerance = 5; // 5 degrees
            
            var xDiff = Math.Abs(x - piece.CorrectX);
            var yDiff = Math.Abs(y - piece.CorrectY);
            var rotDiff = Math.Abs(rotation % 360); // Normalize rotation
            
            return xDiff <= PositionTolerance && 
                   yDiff <= PositionTolerance && 
                   rotDiff <= RotationTolerance;
        }
    }
}