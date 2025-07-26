#!/bin/bash

echo "Starting local development environment..."

# Function to check if port is in use
check_port() {
    if lsof -Pi :$1 -sTCP:LISTEN -t >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Check if Redis is already running
if check_port 6379; then
    echo "Redis is already running on port 6379"
else
    echo "Starting Redis..."
    docker run -d --name puzzle-redis -p 6379:6379 redis:7-alpine || echo "Redis container already exists"
fi

# Build the project
echo "Building the project..."
cd src/CollaborativePuzzle.Api
dotnet build

# Check build result
if [ $? -ne 0 ]; then
    echo "Build failed! Please fix compilation errors."
    exit 1
fi

# Run the API
echo "Starting API on http://localhost:5000..."
echo "Swagger UI: http://localhost:5000/swagger"
echo "Test Page: http://localhost:5000/test.html"
echo "Health Check: http://localhost:5000/api/test/health"
echo ""
echo "Press Ctrl+C to stop..."

# Run with minimal Redis config
ASPNETCORE_URLS="http://localhost:5000" \
ConnectionStrings__Redis="localhost:6379" \
dotnet run --no-build