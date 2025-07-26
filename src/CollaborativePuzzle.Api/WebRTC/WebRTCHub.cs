using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CollaborativePuzzle.Api.WebRTC
{
    /// <summary>
    /// SignalR hub for WebRTC signaling
    /// </summary>
    public class WebRTCHub : Hub
    {
        private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
        private static readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly ILogger<WebRTCHub> _logger;

        public WebRTCHub(ILogger<WebRTCHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? Context.ConnectionId;
            _connections[Context.ConnectionId] = new UserConnection
            {
                ConnectionId = Context.ConnectionId,
                UserId = userId,
                ConnectedAt = DateTime.UtcNow
            };

            _logger.LogInformation("WebRTC client connected: {ConnectionId}", Context.ConnectionId);
            
            await Clients.Caller.SendAsync("Connected", new
            {
                connectionId = Context.ConnectionId,
                userId = userId,
                timestamp = DateTime.UtcNow
            });

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connections.TryRemove(Context.ConnectionId, out var connection))
            {
                // Remove from any rooms
                foreach (var room in _rooms.Values)
                {
                    if (room.Participants.Remove(Context.ConnectionId))
                    {
                        await Clients.Group(room.RoomId).SendAsync("UserLeft", new
                        {
                            userId = connection.UserId,
                            roomId = room.RoomId,
                            timestamp = DateTime.UtcNow
                        });

                        // Clean up empty rooms
                        if (room.Participants.Count == 0)
                        {
                            _rooms.TryRemove(room.RoomId, out _);
                        }
                    }
                }
            }

            _logger.LogInformation("WebRTC client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a room for WebRTC communication
        /// </summary>
        public async Task<RoomJoinResult> JoinRoom(string roomId)
        {
            try
            {
                if (!_connections.TryGetValue(Context.ConnectionId, out var connection))
                {
                    return new RoomJoinResult { Success = false, Error = "Connection not found" };
                }

                // Create or get room
                var room = _rooms.GetOrAdd(roomId, id => new Room
                {
                    RoomId = id,
                    CreatedAt = DateTime.UtcNow,
                    Participants = new HashSet<string>()
                });

                // Add to SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                
                // Add to room
                room.Participants.Add(Context.ConnectionId);

                // Get other participants
                var otherParticipants = room.Participants
                    .Where(p => p != Context.ConnectionId)
                    .Select(p => _connections.TryGetValue(p, out var c) ? c.UserId : p)
                    .ToList();

                // Notify others
                await Clients.OthersInGroup(roomId).SendAsync("UserJoined", new
                {
                    userId = connection.UserId,
                    connectionId = Context.ConnectionId,
                    roomId = roomId,
                    timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} joined room {RoomId}", connection.UserId, roomId);

                return new RoomJoinResult
                {
                    Success = true,
                    RoomId = roomId,
                    Participants = otherParticipants,
                    IceServers = GetIceServers()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room {RoomId}", roomId);
                return new RoomJoinResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Leave a room
        /// </summary>
        public async Task LeaveRoom(string roomId)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var connection))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

                if (_rooms.TryGetValue(roomId, out var room))
                {
                    room.Participants.Remove(Context.ConnectionId);

                    await Clients.Group(roomId).SendAsync("UserLeft", new
                    {
                        userId = connection.UserId,
                        roomId = roomId,
                        timestamp = DateTime.UtcNow
                    });

                    // Clean up empty rooms
                    if (room.Participants.Count == 0)
                    {
                        _rooms.TryRemove(roomId, out _);
                    }
                }

                _logger.LogInformation("User {UserId} left room {RoomId}", connection.UserId, roomId);
            }
        }

        /// <summary>
        /// Send offer to a specific user
        /// </summary>
        public async Task SendOffer(string targetConnectionId, RTCSessionDescription offer)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogDebug("Sending offer from {Sender} to {Target}", sender.UserId, targetConnectionId);
                
                await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    offer = offer,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Send answer to a specific user
        /// </summary>
        public async Task SendAnswer(string targetConnectionId, RTCSessionDescription answer)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogDebug("Sending answer from {Sender} to {Target}", sender.UserId, targetConnectionId);
                
                await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    answer = answer,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Send ICE candidate to a specific user
        /// </summary>
        public async Task SendIceCandidate(string targetConnectionId, RTCIceCandidate candidate)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogDebug("Sending ICE candidate from {Sender} to {Target}", sender.UserId, targetConnectionId);
                
                await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    candidate = candidate,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Request to start a call with another user
        /// </summary>
        public async Task RequestCall(string targetConnectionId, CallRequest request)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("Call request from {Sender} to {Target}", sender.UserId, targetConnectionId);
                
                await Clients.Client(targetConnectionId).SendAsync("IncomingCall", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    request = request,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Respond to a call request
        /// </summary>
        public async Task RespondToCall(string targetConnectionId, CallResponse response)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("Call response from {Sender} to {Target}: {Accepted}", 
                    sender.UserId, targetConnectionId, response.Accepted);
                
                await Clients.Client(targetConnectionId).SendAsync("CallResponse", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    response = response,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// End an active call
        /// </summary>
        public async Task EndCall(string targetConnectionId)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("Call ended by {Sender} with {Target}", sender.UserId, targetConnectionId);
                
                await Clients.Client(targetConnectionId).SendAsync("CallEnded", new
                {
                    from = Context.ConnectionId,
                    fromUserId = sender.UserId,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get list of online users
        /// </summary>
        public Task<List<OnlineUser>> GetOnlineUsers()
        {
            var users = _connections.Values
                .Select(c => new OnlineUser
                {
                    UserId = c.UserId,
                    ConnectionId = c.ConnectionId,
                    ConnectedAt = c.ConnectedAt
                })
                .ToList();

            return Task.FromResult(users);
        }

        /// <summary>
        /// Get ICE server configuration
        /// </summary>
        private List<IceServer> GetIceServers()
        {
            // In production, these would come from configuration
            return new List<IceServer>
            {
                new IceServer
                {
                    Urls = new[] { "stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302" }
                },
                new IceServer
                {
                    Urls = new[] { "turn:localhost:3478" },
                    Username = "puzzle",
                    Credential = "puzzle123"
                }
            };
        }
    }

    // Models
    public class UserConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
    }

    public class Room
    {
        public string RoomId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public HashSet<string> Participants { get; set; } = new();
    }

    public class RoomJoinResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new();
        public List<IceServer> IceServers { get; set; } = new();
    }

    public class RTCSessionDescription
    {
        public string Type { get; set; } = string.Empty; // "offer" or "answer"
        public string Sdp { get; set; } = string.Empty;
    }

    public class RTCIceCandidate
    {
        public string Candidate { get; set; } = string.Empty;
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
        public string? UsernameFragment { get; set; }
    }

    public class IceServer
    {
        public string[] Urls { get; set; } = Array.Empty<string>();
        public string? Username { get; set; }
        public string? Credential { get; set; }
    }

    public class CallRequest
    {
        public string CallType { get; set; } = "video"; // "audio" or "video"
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class CallResponse
    {
        public bool Accepted { get; set; }
        public string? Reason { get; set; }
    }

    public class OnlineUser
    {
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
    }
}