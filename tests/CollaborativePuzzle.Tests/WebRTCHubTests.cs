using CollaborativePuzzle.Api.WebRTC;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CollaborativePuzzle.Tests
{
    public class WebRTCHubTests : TestBase
    {
        private readonly Mock<ILogger<WebRTCHub>> _mockLogger;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<HubCallerContext> _mockContext;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly WebRTCHub _hub;

        public WebRTCHubTests()
        {
            _mockLogger = new Mock<ILogger<WebRTCHub>>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockContext = new Mock<HubCallerContext>();
            _mockGroups = new Mock<IGroupManager>();

            _hub = new WebRTCHub(_mockLogger.Object)
            {
                Clients = _mockClients.Object,
                Context = _mockContext.Object,
                Groups = _mockGroups.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_SendsConnectedMessageToCaller()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var mockCaller = new Mock<IClientProxy>();
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockContext.Setup(x => x.UserIdentifier).Returns((string?)null);
            _mockClients.Setup(x => x.Caller).Returns(mockCaller.Object);

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            mockCaller.Verify(x => x.SendCoreAsync(
                "Connected",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null &&
                    args[0].GetType().GetProperty("connectionId") != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task JoinRoom_SuccessfullyJoinsRoom()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var roomId = "test-room";
            var mockCaller = new Mock<IClientProxy>();
            var mockOthers = new Mock<IClientProxy>();
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockClients.Setup(x => x.Caller).Returns(mockCaller.Object);
            _mockClients.Setup(x => x.OthersInGroup(roomId)).Returns(mockOthers.Object);
            _mockGroups.Setup(x => x.AddToGroupAsync(connectionId, roomId, default))
                .Returns(Task.CompletedTask);

            // Pre-populate connection
            await _hub.OnConnectedAsync();

            // Act
            var result = await _hub.JoinRoom(roomId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(roomId, result.RoomId);
            Assert.NotNull(result.IceServers);
            Assert.NotEmpty(result.IceServers);
            
            _mockGroups.Verify(x => x.AddToGroupAsync(connectionId, roomId, default), Times.Once);
            
            mockOthers.Verify(x => x.SendCoreAsync(
                "UserJoined",
                It.IsAny<object[]>(),
                default),
                Times.Once);
        }

        [Fact]
        public async Task JoinRoom_FailsWhenConnectionNotFound()
        {
            // Arrange
            var roomId = "test-room";
            _mockContext.Setup(x => x.ConnectionId).Returns("unknown-connection");

            // Act
            var result = await _hub.JoinRoom(roomId);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Connection not found", result.Error);
        }

        [Fact]
        public async Task LeaveRoom_RemovesUserFromRoom()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var roomId = "test-room";
            var mockGroup = new Mock<IClientProxy>();
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockClients.Setup(x => x.Group(roomId)).Returns(mockGroup.Object);
            _mockGroups.Setup(x => x.RemoveFromGroupAsync(connectionId, roomId, default))
                .Returns(Task.CompletedTask);

            // Setup: Connect and join room first
            await _hub.OnConnectedAsync();
            await _hub.JoinRoom(roomId);

            // Act
            await _hub.LeaveRoom(roomId);

            // Assert
            _mockGroups.Verify(x => x.RemoveFromGroupAsync(connectionId, roomId, default), Times.Once);
            
            mockGroup.Verify(x => x.SendCoreAsync(
                "UserLeft",
                It.IsAny<object[]>(),
                default),
                Times.Once);
        }

        [Fact]
        public async Task SendOffer_ForwardsOfferToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            var offer = new RTCSessionDescription { Type = "offer", Sdp = "test-sdp" };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.SendOffer(targetConnectionId, offer);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "ReceiveOffer",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task SendAnswer_ForwardsAnswerToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            var answer = new RTCSessionDescription { Type = "answer", Sdp = "test-sdp" };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.SendAnswer(targetConnectionId, answer);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "ReceiveAnswer",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task SendIceCandidate_ForwardsIceCandidateToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            var candidate = new RTCIceCandidate 
            { 
                Candidate = "candidate:123", 
                SdpMid = "0", 
                SdpMLineIndex = 0 
            };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.SendIceCandidate(targetConnectionId, candidate);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "ReceiveIceCandidate",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task RequestCall_SendsIncomingCallToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            var request = new CallRequest { CallType = "video" };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.RequestCall(targetConnectionId, request);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "IncomingCall",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task RespondToCall_SendsCallResponseToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            var response = new CallResponse { Accepted = true };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.RespondToCall(targetConnectionId, response);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "CallResponse",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task EndCall_SendsCallEndedToTarget()
        {
            // Arrange
            var senderConnectionId = "sender-123";
            var targetConnectionId = "target-456";
            var mockTarget = new Mock<IClientProxy>();
            
            _mockContext.Setup(x => x.ConnectionId).Returns(senderConnectionId);
            _mockClients.Setup(x => x.Client(targetConnectionId)).Returns(mockTarget.Object);

            // Setup: Connect first
            await _hub.OnConnectedAsync();

            // Act
            await _hub.EndCall(targetConnectionId);

            // Assert
            mockTarget.Verify(x => x.SendCoreAsync(
                "CallEnded",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null),
                default),
                Times.Once);
        }

        [Fact]
        public async Task GetOnlineUsers_ReturnsConnectedUsers()
        {
            // Arrange
            var connectionId1 = "connection-1";
            var connectionId2 = "connection-2";
            
            _mockContext.SetupSequence(x => x.ConnectionId)
                .Returns(connectionId1)
                .Returns(connectionId2);
            
            _mockContext.Setup(x => x.UserIdentifier).Returns((string?)null);
            _mockClients.Setup(x => x.Caller).Returns(Mock.Of<IClientProxy>());

            // Connect two users
            await _hub.OnConnectedAsync();
            
            // Create new hub instance for second user
            var hub2 = new WebRTCHub(_mockLogger.Object)
            {
                Clients = _mockClients.Object,
                Context = _mockContext.Object,
                Groups = _mockGroups.Object
            };
            await hub2.OnConnectedAsync();

            // Act
            var users = await _hub.GetOnlineUsers();

            // Assert
            Assert.NotNull(users);
            Assert.True(users.Count >= 1); // At least one user connected
        }

        [Fact]
        public async Task OnDisconnectedAsync_RemovesUserFromRoomsAndNotifiesOthers()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var roomId = "test-room";
            var mockGroup = new Mock<IClientProxy>();
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockClients.Setup(x => x.Group(roomId)).Returns(mockGroup.Object);
            _mockGroups.Setup(x => x.AddToGroupAsync(connectionId, roomId, default))
                .Returns(Task.CompletedTask);

            // Setup: Connect and join room
            await _hub.OnConnectedAsync();
            await _hub.JoinRoom(roomId);

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            mockGroup.Verify(x => x.SendCoreAsync(
                "UserLeft",
                It.IsAny<object[]>(),
                default),
                Times.Once);
        }

        [Fact]
        public void GetIceServers_ReturnsValidConfiguration()
        {
            // Arrange & Act
            var result = _hub.JoinRoom("test-room").Result;

            // Assert
            Assert.NotNull(result.IceServers);
            Assert.NotEmpty(result.IceServers);
            
            // Verify STUN servers
            var stunServer = result.IceServers.FirstOrDefault(s => 
                s.Urls.Any(u => u.StartsWith("stun:")));
            Assert.NotNull(stunServer);
            
            // Verify TURN servers
            var turnServer = result.IceServers.FirstOrDefault(s => 
                s.Urls.Any(u => u.StartsWith("turn:")));
            Assert.NotNull(turnServer);
            Assert.NotNull(turnServer.Username);
            Assert.NotNull(turnServer.Credential);
        }
    }
}