#!/bin/bash

# Docker Development Environment Setup Script
set -e

echo "🚀 Starting Collaborative Puzzle Platform Development Environment..."

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to check if a service is healthy
check_service() {
    local service=$1
    local max_attempts=30
    local attempt=0
    
    echo -e "${YELLOW}Waiting for $service to be healthy...${NC}"
    
    while [ $attempt -lt $max_attempts ]; do
        if docker-compose -f docker-compose.dev.yml ps | grep -q "$service.*healthy"; then
            echo -e "${GREEN}✓ $service is healthy${NC}"
            return 0
        fi
        
        attempt=$((attempt + 1))
        sleep 2
    done
    
    echo -e "${RED}✗ $service failed to become healthy${NC}"
    return 1
}

# Stop any existing containers
echo "Stopping existing containers..."
docker-compose -f docker-compose.dev.yml down

# Build the API image
echo "Building API image..."
docker build -f Dockerfile.minimal -t puzzle-api:dev .

# Start infrastructure services first
echo "Starting infrastructure services..."
docker-compose -f docker-compose.dev.yml up -d sqlserver redis mqtt

# Wait for infrastructure to be ready
check_service "puzzle-sqlserver"
check_service "puzzle-redis"

# Start TURN server
echo "Starting TURN server..."
docker-compose -f docker-compose.dev.yml up -d coturn

# Start API
echo "Starting API service..."
docker-compose -f docker-compose.dev.yml up -d api

# Wait for API to be ready
sleep 5

# Show status
echo -e "\n${GREEN}All services started!${NC}"
docker-compose -f docker-compose.dev.yml ps

# Show logs
echo -e "\n${YELLOW}API Logs:${NC}"
docker-compose -f docker-compose.dev.yml logs --tail=20 api

# Show access URLs
echo -e "\n${GREEN}Access URLs:${NC}"
echo "  API:              http://localhost:5000"
echo "  SignalR Test:     http://localhost:5000/test.html"
echo "  WebSocket Test:   http://localhost:5000/websocket-test.html"
echo "  WebRTC Test:      http://localhost:5000/webrtc-test.html"
echo "  MQTT Dashboard:   http://localhost:5000/mqtt-dashboard.html"
echo "  Socket.IO Test:   http://localhost:5000/socketio-test.html"
echo "  Health Check:     http://localhost:5000/health"
echo "  Redis:            localhost:6379"
echo "  SQL Server:       localhost:1433"
echo "  MQTT:             localhost:1883"

echo -e "\n${YELLOW}To view logs:${NC} docker-compose -f docker-compose.dev.yml logs -f"
echo -e "${YELLOW}To stop:${NC} docker-compose -f docker-compose.dev.yml down"