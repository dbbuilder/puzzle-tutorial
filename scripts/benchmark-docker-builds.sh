#!/bin/bash

# Docker Build Performance Benchmark Script

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}Docker Build Performance Benchmark${NC}"
echo "===================================="
echo ""

# Function to build and measure time
benchmark_build() {
    local dockerfile=$1
    local tag=$2
    local description=$3
    
    echo -e "${YELLOW}Building with $dockerfile ($description)...${NC}"
    
    # Clear any existing image
    docker rmi $tag 2>/dev/null || true
    
    # Measure build time
    local start_time=$(date +%s)
    
    if docker build -f $dockerfile -t $tag . --no-cache > /tmp/${tag}_build.log 2>&1; then
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        
        # Get image size
        local size=$(docker images $tag --format "{{.Size}}")
        
        echo -e "${GREEN}✓ Success${NC}"
        echo "  Time: ${duration}s"
        echo "  Size: $size"
        
        # Save results
        echo "$dockerfile,$description,$duration,$size" >> benchmark_results.csv
    else
        echo -e "${RED}✗ Failed${NC}"
        echo "  See /tmp/${tag}_build.log for details"
        tail -20 /tmp/${tag}_build.log
    fi
    
    echo ""
}

# Initialize results file
echo "Dockerfile,Description,BuildTime(s),ImageSize" > benchmark_results.csv

# Run benchmarks
echo "Starting benchmarks at $(date)"
echo ""

# Test each Dockerfile
benchmark_build "Dockerfile.minimal" "puzzle-minimal" "Original Minimal"
benchmark_build "Dockerfile.optimized" "puzzle-optimized" "Alpine Optimized"
benchmark_build "Dockerfile.fast" "puzzle-fast" "Chiseled Fast"
benchmark_build "Dockerfile.dev" "puzzle-dev" "Dev with Cache"

# Show summary
echo -e "${BLUE}Benchmark Results:${NC}"
echo "=================="
column -t -s',' benchmark_results.csv

# Calculate improvements
if [ -f benchmark_results.csv ]; then
    echo ""
    echo -e "${BLUE}Performance Analysis:${NC}"
    
    # Get baseline time (first build)
    baseline=$(awk -F',' 'NR==2 {print $3}' benchmark_results.csv)
    
    # Calculate improvements
    awk -F',' -v baseline="$baseline" '
    NR>1 {
        improvement = ((baseline - $3) / baseline) * 100
        printf "%-20s: %3.0f%% faster\n", $2, improvement
    }' benchmark_results.csv
fi

# Test with build cache (second run)
echo ""
echo -e "${BLUE}Testing with Docker build cache:${NC}"
echo "================================"

# Quick test with cache
for dockerfile in Dockerfile.minimal Dockerfile.optimized Dockerfile.fast Dockerfile.dev; do
    tag=$(echo $dockerfile | sed 's/Dockerfile\./puzzle-/;s/Dockerfile/puzzle/')
    echo -n "Re-building $dockerfile with cache: "
    
    start_time=$(date +%s)
    if docker build -f $dockerfile -t ${tag}-cached . -q >/dev/null 2>&1; then
        end_time=$(date +%s)
        duration=$((end_time - start_time))
        echo -e "${GREEN}${duration}s${NC}"
    else
        echo -e "${RED}Failed${NC}"
    fi
done

# Show docker images
echo ""
echo -e "${BLUE}Image Sizes:${NC}"
docker images | grep puzzle- | sort -k7 -h

# Cleanup option
echo ""
read -p "Clean up test images? (y/N) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    docker images | grep puzzle- | awk '{print $3}' | xargs docker rmi -f 2>/dev/null || true
    echo "Images removed."
fi

echo ""
echo "Benchmark complete! Results saved to benchmark_results.csv"