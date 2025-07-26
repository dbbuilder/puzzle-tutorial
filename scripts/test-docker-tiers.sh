#!/bin/bash

# Docker Tiered Testing Script
# Tests each tier incrementally to identify issues

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to wait for service
wait_for_service() {
    local service=$1
    local port=$2
    local max_attempts=30
    local attempt=0
    
    print_status "$YELLOW" "⏳ Waiting for $service on port $port..."
    
    while [ $attempt -lt $max_attempts ]; do
        if nc -z localhost $port 2>/dev/null; then
            print_status "$GREEN" "✓ $service is ready on port $port"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 2
    done
    
    print_status "$RED" "✗ $service failed to start on port $port"
    return 1
}

# Function to test tier
test_tier() {
    local tier=$1
    local compose_file=$2
    shift 2
    local services=("$@")
    
    print_status "$BLUE" "\n🚀 Testing Tier $tier..."
    print_status "$BLUE" "Using: $compose_file"
    
    # Start services
    print_status "$YELLOW" "Starting services..."
    docker-compose -f $compose_file up -d
    
    # Wait for services
    for service in "${services[@]}"; do
        IFS=':' read -r name port <<< "$service"
        wait_for_service "$name" "$port"
    done
    
    # Run tier-specific tests
    case $tier in
        1)
            print_status "$YELLOW" "Testing Redis connectivity..."
            if docker exec puzzle-redis redis-cli ping | grep -q PONG; then
                print_status "$GREEN" "✓ Redis is responding"
            else
                print_status "$RED" "✗ Redis test failed"
                return 1
            fi
            ;;
        2)
            print_status "$YELLOW" "Testing SQL Server connectivity..."
            if docker exec puzzle-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -Q "SELECT 1" >/dev/null 2>&1; then
                print_status "$GREEN" "✓ SQL Server is responding"
            else
                print_status "$RED" "✗ SQL Server test failed"
                return 1
            fi
            
            print_status "$YELLOW" "Testing MQTT connectivity..."
            if nc -z localhost 1883; then
                print_status "$GREEN" "✓ MQTT broker is listening"
            else
                print_status "$RED" "✗ MQTT test failed"
                return 1
            fi
            ;;
        3)
            print_status "$YELLOW" "Testing API health endpoint..."
            sleep 10  # Give API time to start
            if curl -f http://localhost:5000/health >/dev/null 2>&1; then
                print_status "$GREEN" "✓ API health check passed"
            else
                print_status "$RED" "✗ API health check failed"
                docker-compose -f $compose_file logs api
                return 1
            fi
            
            print_status "$YELLOW" "Testing SignalR hub..."
            if curl -f http://localhost:5000/test.html >/dev/null 2>&1; then
                print_status "$GREEN" "✓ SignalR test page accessible"
            else
                print_status "$RED" "✗ SignalR test page not accessible"
            fi
            ;;
        4)
            print_status "$YELLOW" "Testing full stack..."
            sleep 15  # Give all services time to start
            
            # Test all endpoints
            local endpoints=(
                "http://localhost:5000/health:Health Check"
                "http://localhost:5000/test.html:SignalR Test"
                "http://localhost:5000/websocket-test.html:WebSocket Test"
                "http://localhost:5000/webrtc-test.html:WebRTC Test"
                "http://localhost:5000/mqtt-dashboard.html:MQTT Dashboard"
                "http://localhost:5000/socketio-test.html:Socket.IO Test"
            )
            
            for endpoint in "${endpoints[@]}"; do
                IFS=':' read -r url name <<< "$endpoint"
                if curl -f "$url" >/dev/null 2>&1; then
                    print_status "$GREEN" "✓ $name accessible"
                else
                    print_status "$RED" "✗ $name not accessible"
                fi
            done
            ;;
    esac
    
    # Ask to continue
    print_status "$GREEN" "\n✓ Tier $tier tests completed!"
    read -p "Press Enter to stop this tier and continue to next, or Ctrl+C to exit: "
    
    # Stop services
    print_status "$YELLOW" "Stopping tier $tier services..."
    docker-compose -f $compose_file down
    
    return 0
}

# Main execution
print_status "$BLUE" "🧪 Docker Tiered Testing Script"
print_status "$BLUE" "================================\n"

# Check Docker is running
if ! docker info >/dev/null 2>&1; then
    print_status "$RED" "Docker is not running. Please start Docker and try again."
    exit 1
fi

# Clean up any existing containers
print_status "$YELLOW" "Cleaning up existing containers..."
docker-compose -f docker-compose.tier1-redis.yml down 2>/dev/null || true
docker-compose -f docker-compose.tier2-infra.yml down 2>/dev/null || true
docker-compose -f docker-compose.tier3-api.yml down 2>/dev/null || true
docker-compose -f docker-compose.tier4-full.yml down 2>/dev/null || true

# Test each tier
if test_tier 1 "docker-compose.tier1-redis.yml" "Redis:6379"; then
    if test_tier 2 "docker-compose.tier2-infra.yml" "Redis:6379" "SQL-Server:1433" "MQTT:1883"; then
        
        # Build API image before tier 3
        print_status "$YELLOW" "\n📦 Building API Docker image..."
        if docker build -f Dockerfile.minimal -t puzzle-api:latest .; then
            print_status "$GREEN" "✓ API image built successfully"
            
            if test_tier 3 "docker-compose.tier3-api.yml" "Redis:6379" "API:5000"; then
                test_tier 4 "docker-compose.tier4-full.yml" "Redis:6379" "SQL-Server:1433" "MQTT:1883" "API:5000" "TURN:3478"
            fi
        else
            print_status "$RED" "✗ Failed to build API image"
            exit 1
        fi
    fi
fi

print_status "$GREEN" "\n🎉 All tiers tested successfully!"
print_status "$BLUE" "\nTo run the full stack:"
print_status "$YELLOW" "  docker-compose -f docker-compose.tier4-full.yml up"

print_status "$BLUE" "\nTo run a specific tier:"
print_status "$YELLOW" "  docker-compose -f docker-compose.tier[N]-[name].yml up"

print_status "$BLUE" "\nUseful commands:"
print_status "$YELLOW" "  docker-compose -f [file] logs -f          # View logs"
print_status "$YELLOW" "  docker-compose -f [file] ps               # List services"
print_status "$YELLOW" "  docker-compose -f [file] down             # Stop services"