using Microsoft.AspNetCore.SignalR;

namespace CollaborativePuzzle.Api.SocketIO
{
    /// <summary>
    /// SignalR hub that bridges Socket.IO clients with SignalR functionality
    /// </summary>
    public class SocketIOHub : Hub
    {
        private readonly ILogger<SocketIOHub> _logger;

        public SocketIOHub(ILogger<SocketIOHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Socket.IO bridge client connected: {ConnectionId}", Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, "signalr-clients");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Socket.IO bridge client disconnected: {ConnectionId}", Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "signalr-clients");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a room (Socket.IO concept mapped to SignalR groups)
        /// </summary>
        public async Task JoinRoom(string roomName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room-{roomName}");
            await Clients.Caller.SendAsync("JoinedRoom", roomName);
            await Clients.Group($"room-{roomName}").SendAsync("UserJoinedRoom", Context.ConnectionId);
        }

        /// <summary>
        /// Leave a room
        /// </summary>
        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room-{roomName}");
            await Clients.Caller.SendAsync("LeftRoom", roomName);
            await Clients.Group($"room-{roomName}").SendAsync("UserLeftRoom", Context.ConnectionId);
        }

        /// <summary>
        /// Emit an event to all clients (Socket.IO style)
        /// </summary>
        public async Task Emit(string eventName, object data)
        {
            await Clients.All.SendAsync("SocketIOEvent", eventName, data);
        }

        /// <summary>
        /// Emit an event to a specific room
        /// </summary>
        public async Task EmitToRoom(string roomName, string eventName, object data)
        {
            await Clients.Group($"room-{roomName}").SendAsync("SocketIOEvent", eventName, data);
        }

        /// <summary>
        /// Send a message (compatible with Socket.IO message event)
        /// </summary>
        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", Context.ConnectionId, message);
        }

        /// <summary>
        /// Broadcast data to all except sender (Socket.IO broadcast concept)
        /// </summary>
        public async Task Broadcast(string eventName, object data)
        {
            await Clients.Others.SendAsync("SocketIOEvent", eventName, data);
        }

        /// <summary>
        /// Handle custom Socket.IO events
        /// </summary>
        public async Task HandleCustomEvent(string eventName, object data)
        {
            _logger.LogDebug("Custom Socket.IO event: {EventName} from {ConnectionId}", eventName, Context.ConnectionId);
            
            // Process custom events and forward to appropriate handlers
            switch (eventName)
            {
                case "puzzle-move":
                    await HandlePuzzleMove(data);
                    break;
                case "cursor-update":
                    await HandleCursorUpdate(data);
                    break;
                default:
                    // Forward unknown events to all clients
                    await Broadcast(eventName, data);
                    break;
            }
        }

        private async Task HandlePuzzleMove(object data)
        {
            // Integrate with existing puzzle logic
            await Clients.Others.SendAsync("PuzzlePieceMoved", Context.ConnectionId, data);
        }

        private async Task HandleCursorUpdate(object data)
        {
            // Integrate with cursor tracking
            await Clients.Others.SendAsync("CursorPositionUpdated", Context.ConnectionId, data);
        }
    }
}