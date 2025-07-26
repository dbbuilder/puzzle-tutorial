# Multi-stage build for optimal image size and security
# Stage 1: Build environment
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for better caching
COPY ["CollaborativePuzzle.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["stylecop.json", "./"]
COPY [".editorconfig", "./"]

# Copy project files
COPY ["src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj", "src/CollaborativePuzzle.Api/"]
COPY ["src/CollaborativePuzzle.Core/CollaborativePuzzle.Core.csproj", "src/CollaborativePuzzle.Core/"]
COPY ["src/CollaborativePuzzle.Infrastructure/CollaborativePuzzle.Infrastructure.csproj", "src/CollaborativePuzzle.Infrastructure/"]
COPY ["src/CollaborativePuzzle.Hubs/CollaborativePuzzle.Hubs.csproj", "src/CollaborativePuzzle.Hubs/"]
COPY ["tests/CollaborativePuzzle.Tests/CollaborativePuzzle.Tests.csproj", "tests/CollaborativePuzzle.Tests/"]
COPY ["tests/CollaborativePuzzle.IntegrationTests/CollaborativePuzzle.IntegrationTests.csproj", "tests/CollaborativePuzzle.IntegrationTests/"]

# Restore packages
RUN dotnet restore "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj"

# Copy source code
COPY . .

# Build application
WORKDIR "/src/src/CollaborativePuzzle.Api"
RUN dotnet build "CollaborativePuzzle.Api.csproj" -c Release -o /app/build

# Stage 2: Run tests
FROM build AS test
WORKDIR "/src"

# Run unit tests
RUN dotnet test "tests/CollaborativePuzzle.Tests/CollaborativePuzzle.Tests.csproj" \
    -c Release \
    --logger "console;verbosity=detailed" \
    --collect:"XPlat Code Coverage"

# Stage 3: Publish
FROM build AS publish
WORKDIR "/src/src/CollaborativePuzzle.Api"
RUN dotnet publish "CollaborativePuzzle.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 4: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -g 1000 dotnet && \
    useradd -u 1000 -g dotnet -m -s /bin/bash dotnet

# Copy published application
COPY --from=publish /app/publish .
COPY --from=build /src/appsettings.Example.json ./appsettings.json

# Set ownership
RUN chown -R dotnet:dotnet /app

# Switch to non-root user
USER dotnet

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS="http://+:8080;https://+:8081" \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_USE_POLLING_FILE_WATCHER=false \
    DOTNET_RUNNING_IN_CONTAINER=true

# Entry point
ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]