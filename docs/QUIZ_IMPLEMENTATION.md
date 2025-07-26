# Collaborative Puzzle Platform - Quiz & Assessment Implementation

## Table of Contents

1. [Introduction](#introduction)
2. [Module 1: Architecture & Setup Quiz](#module-1-architecture--setup-quiz)
3. [Module 2: Test-Driven Development Quiz](#module-2-test-driven-development-quiz)
4. [Module 3: SignalR & Real-Time Communication Quiz](#module-3-signalr--real-time-communication-quiz)
5. [Module 4: Redis & Caching Quiz](#module-4-redis--caching-quiz)
6. [Module 5: WebSocket Protocols Quiz](#module-5-websocket-protocols-quiz)
7. [Module 6: WebRTC Implementation Quiz](#module-6-webrtc-implementation-quiz)
8. [Module 7: MQTT & IoT Quiz](#module-7-mqtt--iot-quiz)
9. [Module 8: Container & Kubernetes Quiz](#module-8-container--kubernetes-quiz)
10. [Module 9: Security & Authentication Quiz](#module-9-security--authentication-quiz)
11. [Module 10: Production & Monitoring Quiz](#module-10-production--monitoring-quiz)
12. [Final Comprehensive Assessment](#final-comprehensive-assessment)
13. [Practical Coding Challenges](#practical-coding-challenges)
14. [Answer Key](#answer-key)

## Introduction

This comprehensive quiz implementation covers all major topics in the Collaborative Puzzle Platform. Each module contains 10 questions with varying difficulty levels to assess theoretical knowledge and practical understanding.

### Scoring Guide
- **Basic Questions**: 1 point each
- **Intermediate Questions**: 2 points each
- **Advanced Questions**: 3 points each
- **Coding Challenges**: 5 points each

### Passing Criteria
- **Module Quiz**: 70% or higher
- **Final Assessment**: 80% or higher
- **Practical Challenges**: Complete at least 3 successfully

---

## Module 1: Architecture & Setup Quiz

### Q1.1 (Basic): What is the primary benefit of Clean Architecture?
a) Faster compilation times
b) Separation of concerns and independence from frameworks
c) Reduced code size
d) Automatic dependency injection

### Q1.2 (Basic): Which layer should contain business logic in Clean Architecture?
a) Infrastructure layer
b) Presentation layer
c) Core/Domain layer
d) Data access layer

### Q1.3 (Intermediate): In the Collaborative Puzzle Platform, why are interfaces defined in the Core project?
a) To reduce compilation time
b) To enable dependency inversion and testability
c) To simplify the code structure
d) To improve performance

### Q1.4 (Intermediate): What is the purpose of the Directory.Build.props file?
a) To define project-specific dependencies
b) To configure CI/CD pipelines
c) To set common properties across all projects in the solution
d) To manage database connections

### Q1.5 (Advanced): How would you implement a new real-time protocol in the existing architecture?
```csharp
// Which approach follows Clean Architecture principles?

// Option A:
public class MqttService
{
    private readonly SqlConnection _connection;
    // Direct database access
}

// Option B:
public class MqttService : IMqttService
{
    private readonly IPuzzleRepository _repository;
    private readonly ILogger<MqttService> _logger;
    // Uses abstractions
}
```

### Q1.6 (Basic): What pattern is used for dependency injection configuration?
a) Factory Pattern
b) Service Locator
c) Constructor Injection
d) Property Injection

### Q1.7 (Intermediate): Why use Central Package Management?
a) To reduce project file size
b) To ensure consistent package versions across projects
c) To improve build performance
d) To enable hot reload

### Q1.8 (Advanced): Design a new bounded context for user achievements. Which projects would you create?
```
Write the project structure:
- CollaborativePuzzle.Achievements.Core
- ?
- ?
```

### Q1.9 (Intermediate): What is the role of the Infrastructure project?
a) Define business rules
b) Handle UI concerns
c) Implement external service integrations and data access
d) Manage HTTP routing

### Q1.10 (Advanced): Explain how you would add GraphQL support while maintaining Clean Architecture principles.

---

## Module 2: Test-Driven Development Quiz

### Q2.1 (Basic): What is the correct order in TDD?
a) Code → Test → Refactor
b) Test → Code → Refactor
c) Refactor → Test → Code
d) Code → Refactor → Test

### Q2.2 (Basic): What should a unit test NOT do?
a) Test a single unit of functionality
b) Access external resources like databases
c) Run quickly
d) Be deterministic

### Q2.3 (Intermediate): Write a test for the MovePiece method:
```csharp
[Fact]
public async Task MovePiece_WithValidMove_ShouldUpdatePosition()
{
    // Arrange
    var pieceId = "piece1";
    var newPosition = new Position { X = 100, Y = 200 };
    // Complete this test...
}
```

### Q2.4 (Advanced): How would you test SignalR hub methods?
```csharp
public class PuzzleHubTests
{
    // Design the test setup for testing hub methods
    // Consider: mocking clients, groups, context
}
```

### Q2.5 (Intermediate): What is the purpose of test doubles?
a) To make tests run faster
b) To isolate the unit under test from dependencies
c) To reduce code coverage requirements
d) To simplify test writing

### Q2.6 (Basic): Which testing framework is used in the project?
a) NUnit
b) MSTest
c) xUnit
d) Jest

### Q2.7 (Advanced): Implement a test for concurrent piece movements:
```csharp
[Fact]
public async Task MovePiece_ConcurrentMoves_ShouldHandleCorrectly()
{
    // Test that two users moving the same piece
    // results in proper conflict resolution
}
```

### Q2.8 (Intermediate): What is the AAA pattern in testing?
a) Always Assert Anything
b) Arrange, Act, Assert
c) Asynchronous Automated Assertions
d) Action, Assertion, Analysis

### Q2.9 (Basic): Why use FluentAssertions?
a) Better performance
b) More readable test assertions
c) Automatic test generation
d) Parallel test execution

### Q2.10 (Advanced): Design integration tests for the Redis-backed session management.

---

## Module 3: SignalR & Real-Time Communication Quiz

### Q3.1 (Basic): What transport protocols does SignalR support?
a) Only WebSockets
b) WebSockets, Server-Sent Events, Long Polling
c) Only HTTP/2
d) TCP and UDP

### Q3.2 (Intermediate): How does SignalR handle connection resilience?
a) Manual reconnection only
b) Automatic reconnection with exponential backoff
c) No reconnection support
d) Fixed interval reconnection

### Q3.3 (Advanced): Implement a custom IUserIdProvider:
```csharp
public class CustomUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        // Implementation that extracts user ID from JWT token
    }
}
```

### Q3.4 (Basic): What is a SignalR Hub?
a) A message queue
b) A high-level API for real-time communication
c) A database connection pool
d) A caching mechanism

### Q3.5 (Intermediate): How do you scale SignalR across multiple servers?
a) Not possible
b) Using sticky sessions only
c) Using Redis backplane
d) Using file sharing

### Q3.6 (Advanced): Design a presence system showing online users:
```csharp
public class PresenceHub : Hub
{
    // Track user connections across multiple servers
    // Handle connection/disconnection events
    // Notify other users of presence changes
}
```

### Q3.7 (Intermediate): What is the purpose of strongly-typed hubs?
a) Better performance
b) Compile-time safety for client method calls
c) Automatic serialization
d) Reduced bandwidth

### Q3.8 (Basic): How do you send a message to a specific user?
a) Clients.All.SendAsync()
b) Clients.User(userId).SendAsync()
c) Clients.Broadcast.SendAsync()
d) Hub.SendToUser()

### Q3.9 (Advanced): Implement message throttling for SignalR:
```csharp
public class ThrottledPuzzleHub : Hub
{
    // Limit users to 10 piece moves per second
    // Return appropriate error for rate limit exceeded
}
```

### Q3.10 (Intermediate): What happens when a SignalR connection is lost?
a) Data is queued indefinitely
b) Client receives connection close event
c) Server immediately removes the connection
d) Both b and c

---

## Module 4: Redis & Caching Quiz

### Q4.1 (Basic): What data structure would you use for a leaderboard in Redis?
a) String
b) List
c) Sorted Set
d) Hash

### Q4.2 (Intermediate): Implement distributed locking with Redis:
```csharp
public async Task<bool> AcquireLockAsync(string resource, TimeSpan expiry)
{
    // Use SET with NX and EX options
}
```

### Q4.3 (Basic): What is the purpose of Redis persistence?
a) Improve performance
b) Data durability across restarts
c) Reduce memory usage
d) Enable clustering

### Q4.4 (Advanced): Design a session state manager using Redis:
```csharp
public interface ISessionStateManager
{
    Task<T> GetAsync<T>(string sessionId, string key);
    Task SetAsync<T>(string sessionId, string key, T value, TimeSpan? expiry = null);
    Task<bool> ExistsAsync(string sessionId);
    Task ExpireAsync(string sessionId, TimeSpan expiry);
}
```

### Q4.5 (Intermediate): How do you handle Redis connection failures?
a) Retry with exponential backoff
b) Fail immediately
c) Switch to in-memory cache
d) Both a and c with circuit breaker

### Q4.6 (Basic): What Redis data type is best for storing puzzle piece positions?
a) String with JSON
b) Hash with field per piece
c) List of positions
d) Set of coordinates

### Q4.7 (Advanced): Implement cache-aside pattern with stampede protection:
```csharp
public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiry)
{
    // Prevent cache stampede when popular keys expire
}
```

### Q4.8 (Intermediate): What is Redis Pub/Sub used for?
a) Database queries
b) Message broadcasting
c) Data persistence
d) Connection pooling

### Q4.9 (Basic): How do you set a key with expiration?
a) SET key value; EXPIRE key seconds
b) SETEX key seconds value
c) Both a and b
d) SET key value TTL seconds

### Q4.10 (Advanced): Design a Redis-based rate limiter for API endpoints.

---

## Module 5: WebSocket Protocols Quiz

### Q5.1 (Basic): What is the WebSocket handshake?
a) TCP three-way handshake
b) HTTP upgrade from GET request
c) TLS negotiation
d) Custom binary protocol

### Q5.2 (Intermediate): Implement WebSocket message framing:
```csharp
public byte[] CreateWebSocketFrame(string message, WebSocketMessageType type)
{
    // Create a valid WebSocket frame
    // Handle text/binary types
    // Include proper masking for client frames
}
```

### Q5.3 (Basic): What are WebSocket subprotocols?
a) Transport layer protocols
b) Application-level protocols over WebSocket
c) Security protocols
d) Compression algorithms

### Q5.4 (Advanced): Design a custom WebSocket middleware:
```csharp
public class CustomWebSocketMiddleware
{
    // Handle WebSocket upgrade
    // Manage connection lifecycle
    // Route messages to appropriate handlers
}
```

### Q5.5 (Intermediate): How do you handle WebSocket reconnection?
a) Automatic by protocol
b) Implement client-side logic with backoff
c) Server initiates reconnection
d) Not supported

### Q5.6 (Basic): What is the maximum frame size for WebSocket?
a) 64KB
b) 1MB
c) 2^63 bytes
d) Unlimited

### Q5.7 (Advanced): Implement WebSocket compression:
```csharp
public class CompressedWebSocketHandler
{
    // Use permessage-deflate extension
    // Handle compression negotiation
    // Optimize for real-time data
}
```

### Q5.8 (Intermediate): What is the purpose of WebSocket ping/pong?
a) Measure latency
b) Keep connection alive and detect failures
c) Synchronize data
d) Authenticate users

### Q5.9 (Basic): Which status code indicates normal WebSocket closure?
a) 1000
b) 200
c) 101
d) 404

### Q5.10 (Advanced): Design a WebSocket load balancer with session affinity.

---

## Module 6: WebRTC Implementation Quiz

### Q6.1 (Basic): What does STUN server do?
a) Relay media streams
b) Discover public IP address
c) Encrypt communications
d) Store user credentials

### Q6.2 (Intermediate): Implement SDP offer/answer exchange:
```csharp
public class WebRTCSignaling
{
    public async Task<string> CreateOffer(string userId)
    {
        // Generate SDP offer
        // Store in pending connections
        // Return offer SDP
    }
    
    public async Task<string> CreateAnswer(string userId, string offer)
    {
        // Process offer
        // Generate answer
        // Establish connection
    }
}
```

### Q6.3 (Basic): What is ICE in WebRTC?
a) In-Circuit Emulator
b) Interactive Connectivity Establishment
c) Internet Communication Exchange
d) Integrated Communication Engine

### Q6.4 (Advanced): Design a TURN server allocation system:
```csharp
public interface ITurnAllocationService
{
    Task<TurnCredentials> AllocateCredentials(string userId);
    Task<bool> ValidateCredentials(string username, string password);
    Task RevokeCredentials(string userId);
}
```

### Q6.5 (Intermediate): How do you handle NAT traversal?
a) STUN only
b) TURN only
c) ICE candidates with STUN/TURN
d) Direct connection only

### Q6.6 (Basic): What transport does WebRTC use for media?
a) TCP only
b) UDP with SRTP
c) WebSocket
d) HTTP/2

### Q6.7 (Advanced): Implement bandwidth adaptation:
```csharp
public class AdaptiveBitrateController
{
    // Monitor packet loss and jitter
    // Adjust encoding parameters
    // Notify peers of changes
}
```

### Q6.8 (Intermediate): What is a DataChannel in WebRTC?
a) Video stream
b) Audio stream
c) Bidirectional data communication
d) Signaling channel

### Q6.9 (Basic): Which codec is mandatory for WebRTC video?
a) H.264
b) VP8
c) VP9
d) AV1

### Q6.10 (Advanced): Design a scalable WebRTC signaling server with Redis.

---

## Module 7: MQTT & IoT Quiz

### Q7.1 (Basic): What QoS levels does MQTT support?
a) 0 only
b) 0, 1, 2
c) 1, 2, 3
d) 0 to 5

### Q7.2 (Intermediate): Implement MQTT message routing:
```csharp
public class MqttMessageRouter
{
    public async Task RouteMessage(MqttApplicationMessage message)
    {
        // Parse topic
        // Apply topic filters
        // Route to appropriate handlers
    }
}
```

### Q7.3 (Basic): What is MQTT retained message?
a) Message stored in client
b) Last message saved on topic
c) Encrypted message
d) Compressed message

### Q7.4 (Advanced): Design MQTT authentication with JWT:
```csharp
public class MqttJwtAuthHandler : IMqttServerConnectionValidator
{
    public Task ValidateConnectionAsync(MqttConnectionValidatorContext context)
    {
        // Extract JWT from password field
        // Validate token
        // Set user properties
    }
}
```

### Q7.5 (Intermediate): How do you implement MQTT will message?
a) Send on every disconnect
b) Configure in CONNECT packet
c) Publish manually
d) Not supported

### Q7.6 (Basic): What is MQTT topic hierarchy separator?
a) .
b) /
c) \
d) :

### Q7.7 (Advanced): Implement MQTT to SignalR bridge:
```csharp
public class MqttSignalRBridge
{
    // Subscribe to MQTT topics
    // Transform messages
    // Broadcast via SignalR
    // Handle bidirectional flow
}
```

### Q7.8 (Intermediate): What is MQTT QoS 2 guarantee?
a) At most once
b) At least once
c) Exactly once
d) Best effort

### Q7.9 (Basic): How do you subscribe to multiple topics?
a) Multiple SUBSCRIBE packets
b) Single SUBSCRIBE with multiple filters
c) Not possible
d) Use wildcards only

### Q7.10 (Advanced): Design an MQTT broker cluster with load balancing.

---

## Module 8: Container & Kubernetes Quiz

### Q8.1 (Basic): What is a Kubernetes Pod?
a) A cluster node
b) Smallest deployable unit with one or more containers
c) A network policy
d) A storage volume

### Q8.2 (Intermediate): Write a Kubernetes deployment for SignalR:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: signalr-hub
spec:
  # Complete the deployment
  # Consider scaling, affinity, probes
```

### Q8.3 (Basic): What is a Kubernetes Service?
a) Background process
b) Network abstraction for pods
c) Deployment strategy
d) Container runtime

### Q8.4 (Advanced): Design a multi-stage Dockerfile:
```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Optimize for layer caching
# Minimize final image size

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
# Security hardening
```

### Q8.5 (Intermediate): How do you handle persistent storage in Kubernetes?
a) ConfigMap only
b) PersistentVolumeClaim
c) Store in container
d) External database only

### Q8.6 (Basic): What is a Kubernetes ConfigMap?
a) Cluster configuration
b) Key-value configuration data
c) Network map
d) Service mesh

### Q8.7 (Advanced): Implement horizontal pod autoscaling:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: puzzle-api-hpa
spec:
  # Configure metrics
  # Set scaling policies
```

### Q8.8 (Intermediate): What is init container used for?
a) Container initialization
b) Run setup tasks before main containers
c) Health checking
d) Log collection

### Q8.9 (Basic): How do you expose a service externally?
a) NodePort
b) LoadBalancer
c) Ingress
d) All of the above

### Q8.10 (Advanced): Design a blue-green deployment strategy.

---

## Module 9: Security & Authentication Quiz

### Q9.1 (Basic): What is JWT structure?
a) Header.Payload
b) Header.Payload.Signature
c) Encrypted string
d) Base64 encoded JSON

### Q9.2 (Intermediate): Implement JWT refresh token:
```csharp
public class TokenService
{
    public async Task<TokenPair> RefreshToken(string refreshToken)
    {
        // Validate refresh token
        // Generate new access token
        // Optionally rotate refresh token
    }
}
```

### Q9.3 (Basic): What is CORS?
a) Certificate Order Routing System
b) Cross-Origin Resource Sharing
c) Cryptographic Object Reference Standard
d) Client-Origin Request Security

### Q9.4 (Advanced): Design API key authentication with rate limiting:
```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    // Validate API key
    // Apply rate limits per key
    // Track usage metrics
}
```

### Q9.5 (Intermediate): How do you secure SignalR connections?
a) JWT bearer token
b) Cookie authentication
c) Custom headers
d) All of the above

### Q9.6 (Basic): What is SQL injection?
a) Database optimization
b) Malicious SQL in user input
c) Connection pooling
d) Query caching

### Q9.7 (Advanced): Implement content security policy:
```csharp
public class SecurityHeadersMiddleware
{
    // Add CSP headers
    // Configure for WebSocket/SignalR
    // Handle report-uri
}
```

### Q9.8 (Intermediate): What is OAuth 2.0 flow for SPAs?
a) Client credentials
b) Authorization code with PKCE
c) Implicit (deprecated)
d) Password grant

### Q9.9 (Basic): How do you store passwords securely?
a) Plain text
b) MD5 hash
c) Bcrypt/Scrypt/Argon2
d) Base64 encoding

### Q9.10 (Advanced): Design zero-trust security architecture.

---

## Module 10: Production & Monitoring Quiz

### Q10.1 (Basic): What is distributed tracing?
a) Logging errors
b) Tracking requests across services
c) Network monitoring
d) Database profiling

### Q10.2 (Intermediate): Implement health checks:
```csharp
public class RedisHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Check Redis connectivity
        // Verify response time
        // Return appropriate status
    }
}
```

### Q10.3 (Basic): What is circuit breaker pattern?
a) Electrical safety
b) Prevent cascading failures
c) Load balancing
d) Connection pooling

### Q10.4 (Advanced): Design comprehensive monitoring:
```csharp
public class MetricsCollector
{
    // Application metrics
    // Business metrics
    // Infrastructure metrics
    // Custom dashboards
}
```

### Q10.5 (Intermediate): How do you handle graceful shutdown?
a) Kill process immediately
b) Stop accepting new requests, complete existing
c) Restart immediately
d) Not necessary

### Q10.6 (Basic): What is blue-green deployment?
a) Environment colors
b) Two identical environments for zero-downtime
c) Development stages
d) Security levels

### Q10.7 (Advanced): Implement distributed logging:
```csharp
public class StructuredLoggingEnricher : ILogEventEnricher
{
    // Add correlation ID
    // Add user context
    // Add request context
    // Handle PII properly
}
```

### Q10.8 (Intermediate): What is canary deployment?
a) Security testing
b) Gradual rollout to subset of users
c) Full deployment
d) Rollback strategy

### Q10.9 (Basic): What is MTTR?
a) Maximum Time To Respond
b) Mean Time To Recovery
c) Minimum Testing Time Required
d) Monitoring Time Threshold Reached

### Q10.10 (Advanced): Design disaster recovery plan with RTO < 1 hour.

---

## Final Comprehensive Assessment

### Part A: System Design (20 points)

Design a real-time collaborative code editor using the technologies learned:
- Support for multiple users editing simultaneously
- Conflict resolution for concurrent edits
- Syntax highlighting with minimal latency
- Voice chat for collaborators
- Persistent session state

Requirements:
1. Architecture diagram
2. Technology choices with justification
3. Data flow diagram
4. Scaling strategy
5. Security considerations

### Part B: Implementation Challenge (30 points)

Implement a real-time auction system with:
```csharp
public interface IAuctionService
{
    Task<Auction> CreateAuction(CreateAuctionRequest request);
    Task<Bid> PlaceBid(PlaceBidRequest request);
    Task<IEnumerable<Bid>> GetBidHistory(string auctionId);
    Task EndAuction(string auctionId);
}

public interface IAuctionHub
{
    Task JoinAuction(string auctionId);
    Task LeaveAuction(string auctionId);
    Task NotifyBidPlaced(Bid bid);
    Task NotifyAuctionEnded(AuctionResult result);
}
```

Requirements:
1. Real-time bid updates via SignalR
2. Distributed locking for bid processing
3. Redis for caching and pub/sub
4. Handle concurrent bids correctly
5. Implement bid sniping protection

### Part C: Debugging Challenge (20 points)

Given the following production issues, provide investigation steps and solutions:

1. **Issue**: SignalR connections dropping every 30 seconds
   - Investigation steps?
   - Potential causes?
   - Solution?

2. **Issue**: Redis memory usage growing unbounded
   - How to diagnose?
   - Common causes?
   - Remediation?

3. **Issue**: WebRTC connections failing for 20% of users
   - Debugging approach?
   - Likely problems?
   - Fixes?

### Part D: Performance Optimization (15 points)

Optimize the following code:
```csharp
public async Task<IEnumerable<PuzzleDto>> GetPuzzlesForUser(string userId)
{
    var puzzles = await _dbContext.Puzzles.ToListAsync();
    var userPuzzles = new List<PuzzleDto>();
    
    foreach (var puzzle in puzzles)
    {
        var sessions = await _dbContext.Sessions
            .Where(s => s.PuzzleId == puzzle.Id)
            .ToListAsync();
            
        foreach (var session in sessions)
        {
            var participants = await _dbContext.Participants
                .Where(p => p.SessionId == session.Id)
                .ToListAsync();
                
            if (participants.Any(p => p.UserId == userId))
            {
                userPuzzles.Add(new PuzzleDto(puzzle));
            }
        }
    }
    
    return userPuzzles;
}
```

### Part E: Architecture Decision (15 points)

You need to add real-time collaborative whiteboard feature. Evaluate:

1. **SignalR vs Raw WebSocket vs WebRTC DataChannel**
   - Pros/cons of each
   - Recommendation with justification

2. **State Synchronization Approach**
   - Operational Transform vs CRDT
   - Implementation complexity
   - Performance implications

3. **Persistence Strategy**
   - Event sourcing vs snapshot
   - Storage requirements
   - Replay capabilities

---

## Practical Coding Challenges

### Challenge 1: Implement Presence System

Create a presence system showing online users with accurate status:

```csharp
public interface IPresenceService
{
    Task<IEnumerable<string>> GetOnlineUsers();
    Task<bool> IsUserOnline(string userId);
    Task<UserPresence> GetUserPresence(string userId);
    Task UpdateActivity(string userId);
}
```

Requirements:
- Handle multiple connections per user
- Work across scaled instances
- Clean up disconnected users
- Provide last seen timestamp

### Challenge 2: Build Rate Limiter

Implement distributed rate limiting:

```csharp
public interface IRateLimiter
{
    Task<RateLimitResult> CheckLimit(string identifier, string resource);
    Task<RateLimitStatus> GetStatus(string identifier, string resource);
    Task Reset(string identifier, string resource);
}
```

Requirements:
- Sliding window algorithm
- Redis-backed implementation
- Configurable limits per resource
- Return headers for client

### Challenge 3: Create Message Queue

Build a reliable message queue for background jobs:

```csharp
public interface IReliableQueue<T>
{
    Task<string> EnqueueAsync(T message, QueueOptions options);
    Task<QueueMessage<T>> DequeueAsync(string queue);
    Task AcknowledgeAsync(string messageId);
    Task RequeueAsync(string messageId, TimeSpan delay);
}
```

Requirements:
- At-least-once delivery
- Message visibility timeout
- Dead letter queue
- Priority support

### Challenge 4: Develop Circuit Breaker

Implement circuit breaker pattern:

```csharp
public interface ICircuitBreaker
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string resourceName);
    CircuitState GetState(string resourceName);
    Task ResetAsync(string resourceName);
    event EventHandler<CircuitStateChangedEventArgs> StateChanged;
}
```

Requirements:
- Configurable failure threshold
- Half-open state
- Distributed state with Redis
- Metrics emission

### Challenge 5: Build Analytics Pipeline

Create real-time analytics for puzzle completion:

```csharp
public interface IAnalyticsPipeline
{
    Task TrackEvent(AnalyticsEvent evt);
    Task<AnalyticsSummary> GetRealTimeStats(string puzzleId);
    Task<IEnumerable<AnalyticsMetric>> GetMetrics(MetricQuery query);
}
```

Requirements:
- Stream processing
- Time-series data
- Aggregation windows
- Real-time dashboard updates

---

## Answer Key

### Module 1 Answers
1. b) Separation of concerns and independence from frameworks
2. c) Core/Domain layer
3. b) To enable dependency inversion and testability
4. c) To set common properties across all projects in the solution
5. Option B - Uses abstractions and follows dependency inversion
6. c) Constructor Injection
7. b) To ensure consistent package versions across projects
8. Additional projects: CollaborativePuzzle.Achievements.Infrastructure, CollaborativePuzzle.Achievements.Api
9. c) Implement external service integrations and data access
10. Create GraphQL types in Core, resolvers in Infrastructure, endpoint in Api

### Module 2 Answers
1. b) Test → Code → Refactor
2. b) Access external resources like databases
3. Complete test with mocks, assertions, and verifications
4. Mock IHubCallerClients, IHubCallerContext, and related interfaces
5. b) To isolate the unit under test from dependencies
6. c) xUnit
7. Use SemaphoreSlim or distributed locking to test race conditions
8. b) Arrange, Act, Assert
9. b) More readable test assertions
10. Use TestServer, seed Redis, verify state consistency

### Module 3 Answers
1. b) WebSockets, Server-Sent Events, Long Polling
2. b) Automatic reconnection with exponential backoff
3. Extract from Authorization header or query string
4. b) A high-level API for real-time communication
5. c) Using Redis backplane
6. Track connections in Redis, handle OnConnected/OnDisconnected
7. b) Compile-time safety for client method calls
8. b) Clients.User(userId).SendAsync()
9. Use IMemoryCache or Redis to track and limit requests
10. d) Both b and c

### Module 4 Answers
1. c) Sorted Set
2. Use SET with NX and EX flags, return success/failure
3. b) Data durability across restarts
4. Implement with Redis HASH and key expiration
5. d) Both a and c with circuit breaker
6. b) Hash with field per piece
7. Use distributed lock, probabilistic early expiration
8. b) Message broadcasting
9. c) Both a and b
10. Use sliding window or token bucket with Redis

### Module 5 Answers
1. b) HTTP upgrade from GET request
2. Include FIN bit, opcode, payload length, masking
3. b) Application-level protocols over WebSocket
4. Handle upgrade, manage connections, route based on path
5. b) Implement client-side logic with backoff
6. c) 2^63 bytes
7. Negotiate permessage-deflate, handle context takeover
8. b) Keep connection alive and detect failures
9. a) 1000
10. Use consistent hashing or connection ID routing

### Module 6 Answers
1. b) Discover public IP address
2. Store offers in Redis, exchange via SignalR
3. b) Interactive Connectivity Establishment
4. Generate temporary credentials, store in Redis with TTL
5. c) ICE candidates with STUN/TURN
6. b) UDP with SRTP
7. Monitor stats, adjust constraints, renegotiate
8. c) Bidirectional data communication
9. b) VP8
10. Use Redis pub/sub for offer/answer exchange

### Module 7 Answers
1. b) 0, 1, 2
2. Parse topic hierarchy, match wildcards, route
3. b) Last message saved on topic
4. Validate JWT, extract claims, set context properties
5. b) Configure in CONNECT packet
6. b) /
7. Subscribe to MQTT, transform, publish to SignalR
8. c) Exactly once
9. b) Single SUBSCRIBE with multiple filters
10. Use shared subscriptions, session persistence

### Module 8 Answers
1. b) Smallest deployable unit with one or more containers
2. Include replicas, update strategy, resource limits, probes
3. b) Network abstraction for pods
4. Multi-stage with minimal runtime, non-root user
5. b) PersistentVolumeClaim
6. b) Key-value configuration data
7. Configure CPU/memory metrics, behavior policies
8. b) Run setup tasks before main containers
9. d) All of the above
10. Use separate environments, switch traffic via service

### Module 9 Answers
1. b) Header.Payload.Signature
2. Validate against stored token, check expiry, issue new
3. b) Cross-Origin Resource Sharing
4. Extract from header, validate, apply limits
5. d) All of the above
6. b) Malicious SQL in user input
7. Configure nonces, report-uri, WebSocket exceptions
8. b) Authorization code with PKCE
9. c) Bcrypt/Scrypt/Argon2
10. Verify every request, principle of least privilege

### Module 10 Answers
1. b) Tracking requests across services
2. Check connectivity, measure latency, return status
3. b) Prevent cascading failures
4. Use OpenTelemetry, custom metrics, dashboards
5. b) Stop accepting new requests, complete existing
6. b) Two identical environments for zero-downtime
7. Add trace ID, user context, sanitize PII
8. b) Gradual rollout to subset of users
9. b) Mean Time To Recovery
10. Automated backups, geo-replication, runbooks

### Final Assessment Grading Rubric

**Part A (20 points)**
- Architecture clarity: 5 points
- Technology justification: 5 points
- Scalability consideration: 5 points
- Security approach: 5 points

**Part B (30 points)**
- Correct implementation: 15 points
- Concurrency handling: 5 points
- Error handling: 5 points
- Performance: 5 points

**Part C (20 points)**
- Diagnostic approach: 10 points
- Root cause analysis: 5 points
- Solution quality: 5 points

**Part D (15 points)**
- Query optimization: 10 points
- Algorithm efficiency: 5 points

**Part E (15 points)**
- Analysis depth: 10 points
- Decision justification: 5 points

**Total: 100 points**
- Pass: 80+ points
- Merit: 90+ points
- Distinction: 95+ points

---

## Conclusion

This comprehensive quiz implementation covers all aspects of the Collaborative Puzzle Platform, from basic concepts to advanced implementation challenges. Use it to assess understanding and identify areas for further study.

Remember: The goal is not just to answer correctly but to understand the underlying principles and be able to apply them in real-world scenarios.