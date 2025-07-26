# Collaborative Jigsaw Puzzle Platform

## 📚 Interactive Learning Guide

**New to the project? Start with our interactive documentation viewer!**

🚀 **[View the Interactive Learning Guide](https://dbbuilder.github.io/puzzle-tutorial/github-learning-guide.html)**

Our comprehensive learning guide provides:
- ✅ Auto-discovered documentation from this repository
- ✅ Embedded learning resources and implementation checklists
- ✅ Complete bibliography with links to official docs
- ✅ Enterprise architecture patterns with code examples
- ✅ Step-by-step deployment guides
- ✅ 850+ lines of production-ready code samples

**No installation required** - just open in your browser and start learning!

---

## 🧩 Project Overview

A real-time collaborative jigsaw puzzle application built with ASP.NET Core, SignalR, WebRTC, Redis, and Vue.js. Multiple users can simultaneously work on digital puzzles with live synchronization, voice chat, and persistent state management.

This project serves as a comprehensive example of building enterprise-grade applications with modern technologies, demonstrating:
- Microservices architecture
- Real-time communication at scale
- Cloud-native deployment
- Enterprise design patterns
- Production monitoring and observability

## 🎯 Learning Objectives

By studying this project, you'll learn:

1. **System Architecture**
   - Designing scalable microservices
   - Implementing real-time features with SignalR and WebSockets
   - Managing distributed state with Redis

2. **Cloud Technologies**
   - Azure service integration (AKS, SQL, Redis, Blob Storage)
   - Kubernetes deployment and orchestration
   - Infrastructure as Code practices

3. **Modern Development**
   - ASP.NET Core 8.0 with Minimal APIs
   - Vue.js 3 with TypeScript
   - Docker containerization
   - CI/CD pipelines

4. **Enterprise Practices**
   - Security implementation (JWT, Azure AD B2C)
   - Performance optimization
   - Monitoring with Application Insights
   - Production deployment strategies

## 🏗️ Architecture Overview

### System Components
- **API Gateway**: ASP.NET Core Minimal APIs with OpenAPI documentation
- **Real-Time Hub**: SignalR for puzzle state synchronization
- **WebSocket Service**: Direct WebSocket connections for high-frequency piece movements
- **Voice Chat**: WebRTC with STUN/TURN servers for peer-to-peer communication
- **Caching Layer**: Redis for session state and puzzle data
- **Database**: Azure SQL Database with Entity Framework Core stored procedures
- **File Storage**: Azure Blob Storage for puzzle images and generated pieces
- **Frontend**: Vue.js 3 with TypeScript and Tailwind CSS

### Technology Stack

#### Backend
- ASP.NET Core 8.0 (Linux containers)
- Entity Framework Core 8.0 (Stored Procedures only)
- SignalR for real-time communication
- Redis for caching and SignalR backplane
- Azure Blob Storage for file management
- Serilog for structured logging
- Polly for resilience patterns
- HangFire for background job processing

#### Frontend
- Vue.js 3 with Composition API
- TypeScript for type safety
- Tailwind CSS for styling
- Pinia for state management
- SignalR JavaScript client
- Simple-peer for WebRTC integration

#### Infrastructure
- Docker containers for all services
- Azure Kubernetes Service (AKS)
- Azure Application Gateway with WebSocket support
- Azure Key Vault for secret management
- Azure Application Insights for monitoring

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+ and npm
- Docker Desktop
- Azure CLI (for deployment)
- Redis (local or Azure Cache for Redis)
- SQL Server (local or Azure SQL Database)

### Local Development Setup

#### 1. Clone and Setup Backend
```bash
git clone <repository-url>
cd CollaborativePuzzle

# Create directory structure
mkdir -p src/CollaborativePuzzle.Api
mkdir -p src/CollaborativePuzzle.Core
mkdir -p src/CollaborativePuzzle.Infrastructure
mkdir -p src/CollaborativePuzzle.Hubs
mkdir -p src/CollaborativePuzzle.Frontend
mkdir -p scripts
mkdir -p k8s
mkdir -p docs
```

#### 2. Install NuGet Packages
```bash
cd src/CollaborativePuzzle.Api
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.AspNetCore.SignalR.Redis
dotnet add package StackExchange.Redis
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.ApplicationInsights
dotnet add package Polly
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer
dotnet add package Swashbuckle.AspNetCore
dotnet add package MessagePack.AspNetCore
```

#### 3. Setup Frontend
```bash
cd src/CollaborativePuzzle.Frontend
npm init vue@latest . --typescript --router --pinia
npm install @microsoft/signalr simple-peer
npm install -D @types/simple-peer
npm install tailwindcss @tailwindcss/typography @tailwindcss/forms
```

#### 4. Configure Local Environment
```bash
# Copy example configuration
cp appsettings.example.json appsettings.Development.json

# Update connection strings and configuration
# See Configuration section below
```

#### 5. Run Services
```bash
# Start local Redis (if using Docker)
docker run -d -p 6379:6379 redis:alpine

# Start SQL Server (if using Docker)
docker run -d -p 1433:1433 -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" mcr.microsoft.com/mssql/server:2022-latest

# Run database migrations
cd src/CollaborativePuzzle.Api
dotnet ef database update

# Start API
dotnet run

# Start Frontend (separate terminal)
cd src/CollaborativePuzzle.Frontend
npm run dev
```

### Docker Development

```bash
# Build and run all services
docker-compose -f docker-compose.dev.yml up --build

# Access application
# Frontend: http://localhost:3000
# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

## 📖 Documentation

### Comprehensive Guides
- **[Interactive Learning Guide](https://dbbuilder.github.io/puzzle-tutorial/github-learning-guide.html)** - Start here!
- [Architecture Overview](docs/ARCHITECTURE_OVERVIEW.md) - System design and components
- [Enterprise Patterns](docs/ENTERPRISE_PATTERNS.md) - Design patterns with examples
- [Code Samples](docs/CODE_SAMPLES.md) - 850+ lines of production code
- [Production Deployment](docs/PRODUCTION_DEPLOYMENT.md) - Complete Azure deployment
- [Monitoring & Observability](docs/MONITORING_OBSERVABILITY.md) - Logging and metrics

### Technology Guides
- [SignalR & Redis Guide](docs/SIGNALR_REDIS_GUIDE.md) - Real-time communication
- [WebRTC Guide](docs/WEBRTC_GUIDE.md) - Voice chat implementation
- [Kubernetes Architecture](docs/KUBERNETES_ARCHITECTURE.md) - Container orchestration
- [Configuration Guide](docs/CONFIGURATION_FILES_GUIDE.md) - Settings management
- [TDD Guide](docs/TDD_GUIDE.md) - Test-driven development

## 🔧 Configuration

### Required Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CollaborativePuzzle;Trusted_Connection=true;TrustServerCertificate=true;",
    "Redis": "localhost:6379"
  },
  "AzureKeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"
  },
  "SignalR": {
    "RedisConnectionString": "localhost:6379"
  },
  "WebRTC": {
    "IceServers": [
      { "urls": "stun:stun.l.google.com:19302" },
      { 
        "urls": "turn:your-turn-server.com:3478",
        "username": "username",
        "credential": "password"
      }
    ]
  },
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key"
  }
}
```

### Azure Key Vault Secrets
- `SqlConnectionString`: Database connection string
- `RedisConnectionString`: Redis cache connection string
- `BlobStorageConnectionString`: Azure Blob Storage connection string
- `TurnServerCredentials`: WebRTC TURN server authentication

## 📡 API Documentation

### Core Endpoints

#### Puzzle Management
- `GET /api/puzzles` - List available puzzles
- `POST /api/puzzles` - Create new puzzle with image upload
- `GET /api/puzzles/{id}` - Get puzzle details and piece data
- `DELETE /api/puzzles/{id}` - Delete puzzle (owner only)

#### Session Management
- `POST /api/sessions` - Create new puzzle session
- `GET /api/sessions/{id}` - Get session details and participants
- `POST /api/sessions/{id}/join` - Join existing session
- `DELETE /api/sessions/{id}/leave` - Leave session

#### User Management
- `GET /api/users/profile` - Get current user profile
- `PUT /api/users/profile` - Update user profile
- `GET /api/users/{id}/sessions` - Get user's active sessions

### SignalR Hub Endpoints

#### PuzzleHub (`/puzzlehub`)
- `JoinPuzzleSession(sessionId)` - Join puzzle session
- `MovePiece(pieceId, x, y, rotation)` - Move puzzle piece
- `LockPiece(pieceId)` - Lock piece for editing
- `UnlockPiece(pieceId)` - Release piece lock
- `SendChatMessage(message)` - Send chat message to session
- `UpdateCursor(x, y)` - Update user cursor position

#### Events Received
- `PieceMove` - Another user moved a piece
- `PieceLocked` - Another user locked a piece
- `PieceUnlocked` - Another user unlocked a piece
- `UserJoined` - User joined the session
- `UserLeft` - User left the session
- `ChatMessage` - New chat message received
- `CursorUpdate` - User cursor position update
- `PuzzleCompleted` - Puzzle completion notification

## 💾 Database Schema

### Core Tables
- `Puzzles` - Puzzle metadata and configuration
- `PuzzlePieces` - Individual piece definitions and positions
- `Sessions` - Active puzzle sessions
- `SessionParticipants` - Users in each session
- `ChatMessages` - Session chat history
- `Users` - User profiles and authentication

### Stored Procedures
- `sp_CreatePuzzle` - Create new puzzle with pieces
- `sp_GetPuzzleWithPieces` - Retrieve complete puzzle data
- `sp_UpdatePiecePosition` - Update piece position atomically
- `sp_GetSessionParticipants` - Get all users in session
- `sp_SaveChatMessage` - Store chat message with metadata

## 🚢 Deployment

### Azure Kubernetes Service

```bash
# Create AKS cluster
az aks create --resource-group rg-puzzle --name aks-puzzle --node-count 2 --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group rg-puzzle --name aks-puzzle

# Deploy application
kubectl apply -f k8s/
```

### Required Azure Resources
- Azure Kubernetes Service (AKS)
- Azure SQL Database
- Azure Cache for Redis
- Azure Blob Storage
- Azure Key Vault
- Azure Application Gateway
- Azure Container Registry (ACR)
- Azure Application Insights

## 📊 Monitoring and Logging

### Application Insights Integration
- Request/response tracking
- Exception monitoring
- Performance counters
- Custom telemetry for puzzle events
- User session correlation

### Structured Logging with Serilog
- Request/response logging
- SignalR connection events
- Business logic events
- Performance measurements
- Error tracking with context

### Health Checks
- Database connectivity
- Redis connectivity
- Blob storage accessibility
- SignalR hub health
- WebRTC TURN server availability

## 🧪 Testing

### Unit Tests
```bash
cd tests/CollaborativePuzzle.Tests
dotnet test
```

### Integration Tests
```bash
cd tests/CollaborativePuzzle.IntegrationTests
dotnet test
```

### Frontend Tests
```bash
cd src/CollaborativePuzzle.Frontend
npm run test:unit
npm run test:e2e
```

## ⚡ Performance Considerations

### Optimization Strategies
- Redis caching for frequent puzzle data access
- SignalR connection scaling with Redis backplane
- Efficient WebSocket message serialization using MessagePack
- Database connection pooling with optimal timeout settings
- Image optimization and CDN delivery for puzzle assets

### Scaling Guidelines
- Horizontal pod autoscaling based on CPU and memory usage
- Redis cluster configuration for high availability
- Database read replicas for improved query performance
- Azure Front Door for global content distribution

## 🔒 Security

### Authentication and Authorization
- Azure Active Directory B2C integration
- JWT token validation for API endpoints
- SignalR connection authentication
- Role-based access control for puzzle management

### Security Headers
- Content Security Policy (CSP)
- HTTP Strict Transport Security (HSTS)
- X-Frame-Options and X-Content-Type-Options
- CORS configuration for frontend origins

### Input Validation
- Model validation for all API endpoints
- File upload restrictions and virus scanning
- SQL injection prevention with parameterized procedures
- XSS protection through proper encoding

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Standards
- Follow Microsoft C# coding conventions
- Use TypeScript strict mode for frontend code
- Implement comprehensive error handling and logging
- Write unit tests for all business logic
- Document public APIs with XML comments

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 💬 Support

For questions and support:
- Start with the **[Interactive Learning Guide](https://dbbuilder.github.io/puzzle-tutorial/github-learning-guide.html)**
- Open an issue in the GitHub repository
- Check the comprehensive documentation in the `/docs` folder

### Useful Links
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr)
- [Vue.js Documentation](https://vuejs.org/)
- [Azure Kubernetes Service Documentation](https://docs.microsoft.com/azure/aks)
- [Redis Documentation](https://redis.io/documentation)