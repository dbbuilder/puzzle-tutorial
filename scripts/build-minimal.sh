#!/bin/bash

echo "Building minimal version for testing..."

# Clean previous builds
docker system prune -f

# Build just the API with a simpler approach
docker build -t puzzle-api:minimal -f - . << 'EOF'
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only essential files first
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["*.sln", "./"]

# Copy project files
COPY ["src/CollaborativePuzzle.Api/*.csproj", "src/CollaborativePuzzle.Api/"]
COPY ["src/CollaborativePuzzle.Core/*.csproj", "src/CollaborativePuzzle.Core/"]
COPY ["src/CollaborativePuzzle.Infrastructure/*.csproj", "src/CollaborativePuzzle.Infrastructure/"]
COPY ["src/CollaborativePuzzle.Hubs/*.csproj", "src/CollaborativePuzzle.Hubs/"]

# Restore
RUN dotnet restore "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj" --disable-parallel

# Copy source
COPY . .

# Build without running analyzers
RUN dotnet build "src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj" -c Debug -o /app/build /p:RunAnalyzers=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/build .
COPY ["src/CollaborativePuzzle.Api/appsettings*.json", "./"]

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "CollaborativePuzzle.Api.dll"]
EOF

echo "Build complete. Now running the container..."
docker run --rm -p 8080:8080 --name puzzle-api puzzle-api:minimal