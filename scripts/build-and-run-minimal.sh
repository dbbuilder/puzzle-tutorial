#!/bin/bash

echo "Building minimal version..."

# Use .NET 8.0 SDK for local build
echo "Building with .NET 8.0..."
dotnet clean
dotnet build --configuration Release src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj

if [ $? -ne 0 ]; then
    echo "Build failed"
    exit 1
fi

echo "Starting services with docker-compose..."
docker-compose up -d redis

echo "Waiting for Redis to start..."
sleep 5

echo "Running the API..."
cd src/CollaborativePuzzle.Api
ASPNETCORE_URLS="http://localhost:5000" \
ConnectionStrings__Redis="localhost:6379" \
dotnet run --no-build --configuration Release