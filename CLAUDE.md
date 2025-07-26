# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a real-time collaborative jigsaw puzzle platform built with ASP.NET Core 9.0, demonstrating multiple real-time communication technologies. The project serves as a comprehensive tutorial for SignalR, WebSockets, WebRTC, MQTT, Socket.IO, and QUIC protocols with production-ready patterns.

## Key Technologies Demonstrated

- **SignalR with Redis Backplane**: Scalable real-time communication with distributed locking
- **Raw WebSockets**: Low-level WebSocket implementation for learning
- **WebRTC**: Peer-to-peer voice/video chat with STUN/TURN
- **MQTT**: IoT-style messaging patterns
- **Socket.IO**: Compatibility layer demonstration
- **QUIC/HTTP3**: Next-generation protocol support
- **Kubernetes**: Container orchestration with networking examples
- **Redis**: Caching, pub/sub, and SignalR backplane

## Development Commands

### Building and Running
```bash
# Build the entire solution
dotnet build

# Run the API project
cd src/CollaborativePuzzle.Api
dotnet run

# Run in watch mode for development
dotnet watch run
```

### Testing
```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/CollaborativePuzzle.Tests/CollaborativePuzzle.Tests.csproj

# Run integration tests
dotnet test tests/CollaborativePuzzle.IntegrationTests/CollaborativePuzzle.IntegrationTests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Operations
```bash
# When connecting from WSL to Windows SQL Server, use:
Server: 172.31.208.1,14333  # Use WSL host IP, not localhost
Username: sv
Password: YourPassword  # NO QUOTES around password

# Create migrations (from Api project)
cd src/CollaborativePuzzle.Api
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# SQL command line access from WSL
sqlcmd -S 172.31.208.1,14333 -U sv -P YourPassword -C -d CollaborativePuzzle
```

## Architecture Overview

### Project Structure
- **CollaborativePuzzle.Api**: ASP.NET Core Web API with minimal APIs, endpoints, and configuration
- **CollaborativePuzzle.Core**: Domain entities, interfaces, enums, and business models
- **CollaborativePuzzle.Infrastructure**: EF Core DbContext, repositories, services, and stored procedures
- **CollaborativePuzzle.Hubs**: SignalR hubs for real-time communication (PuzzleHub)
- **CollaborativePuzzle.Tests**: Unit tests using xUnit
- **CollaborativePuzzle.IntegrationTests**: Integration tests

### Key Technologies
- **.NET 9.0** with C# nullable reference types enabled
- **Entity Framework Core** configured for stored procedures only (no LINQ queries)
- **SignalR** for real-time puzzle synchronization
- **Redis** for caching and SignalR backplane
- **Azure Blob Storage** for puzzle images
- **xUnit** for testing framework

### Database Design
The system uses SQL Server with stored procedures for all data access:
- `sp_CreatePuzzle.sql` - Create new puzzles
- `sp_GetPuzzleWithPieces.sql` - Retrieve puzzle data
- `sp_LockPiece.sql` / `sp_UnlockPiece.sql` - Piece locking mechanism
- `sp_UpdatePiecePosition.sql` - Update piece positions

### Core Entities
- **User**: User profiles with authentication
- **Puzzle**: Puzzle metadata and configuration
- **PuzzlePiece**: Individual pieces with positions and lock status
- **PuzzleSession**: Active puzzle-solving sessions
- **SessionParticipant**: Users in sessions with roles and status
- **ChatMessage**: In-session communication

### SignalR Hub (PuzzleHub)
Key methods:
- `JoinPuzzleSession(sessionId)`
- `MovePiece(pieceId, x, y, rotation)`
- `LockPiece(pieceId)` / `UnlockPiece(pieceId)`
- `SendChatMessage(message)`
- `UpdateCursor(x, y)`

## Development Guidelines

### Connection String Configuration
When developing in WSL connecting to Windows SQL Server:
- Use WSL host IP (172.31.208.1) instead of localhost
- Include port if non-standard (e.g., 14333)
- Always add `TrustServerCertificate=true` in connection strings
- Never quote passwords in command-line tools

### Entity Framework Patterns
- All data access through stored procedures
- Use `DbContext.Database.ExecuteSqlRawAsync()` for stored procedure calls
- Row versioning enabled on key entities for concurrency control
- Audit fields (CreatedAt, UpdatedAt) automatically managed

### Real-time Communication
- SignalR configured with Redis backplane for scaling
- MessagePack serialization for performance
- Connection state tracked in SessionParticipant entity
- Automatic reconnection handling required
- Distributed locking via Redis for piece editing
- Cursor updates throttled to 10/second using Channels
- Connection tracking in Redis for resilience

### Testing Approach
- TDD methodology with tests written first
- Unit tests use Moq for dependencies
- Integration tests use TestContainers
- Test base classes in `TestBase` folder
- Fluent test data builders for readability

### Code Quality
- StyleCop and SonarAnalyzer enforced
- Central package management via Directory.Packages.props
- Nullable reference types enabled
- XML documentation required for public APIs

### Error Handling
- Polly resilience patterns for external service calls
- Structured logging with Serilog
- DbUpdateConcurrencyException handling for concurrent edits
- Graceful degradation when Redis unavailable

## Key Implementation Files

### SignalR Hub
- `src/CollaborativePuzzle.Hubs/PuzzleHub.cs` - Main SignalR hub with Redis backplane
- `tests/CollaborativePuzzle.Tests/Hubs/PuzzleHubTests.cs` - Comprehensive hub tests
- `docs/SIGNALR_REDIS_GUIDE.md` - Detailed implementation guide

### Test Infrastructure
- `tests/CollaborativePuzzle.Tests/TestBase/TestBase.cs` - Base test class
- `tests/CollaborativePuzzle.Tests/TestBase/IntegrationTestBase.cs` - Integration test base
- `tests/CollaborativePuzzle.Tests/Helpers/TestDataBuilder.cs` - Fluent test builders

### Documentation
- `docs/TDD_GUIDE.md` - Test-driven development practices
- `docs/SECRETS_MANAGEMENT.md` - Secrets and configuration guide