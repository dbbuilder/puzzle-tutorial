#!/bin/bash

# Measure Docker build improvements

echo "Docker Build Performance Comparison"
echo "==================================="
echo ""

# Function to measure build time
measure_build() {
    local dockerfile=$1
    local name=$2
    
    echo "Building $name..."
    local start=$(date +%s)
    
    # Build with progress output to see what's slow
    if docker build -f $dockerfile -t test-$name . --no-cache --progress=plain > /tmp/${name}.log 2>&1; then
        local end=$(date +%s)
        local duration=$((end - start))
        
        # Get image size
        local size=$(docker images test-$name --format "{{.Size}}")
        
        echo "✓ Success: ${duration}s, Size: $size"
        
        # Extract stage timings from log
        echo "  Stage timings:"
        grep -E "DONE [0-9]+\.[0-9]+s" /tmp/${name}.log | tail -10
        
        # Return duration for comparison
        echo "$duration"
    else
        echo "✗ Failed - check /tmp/${name}.log"
        tail -20 /tmp/${name}.log
        echo "999999"  # Return large number for failed builds
    fi
    echo ""
}

# Test builds
echo "1. Original Dockerfile.minimal"
time1=$(measure_build Dockerfile.minimal minimal | tail -1)

echo "2. Optimized Dockerfile.ultra"  
time2=$(measure_build Dockerfile.ultra ultra | tail -1)

echo "3. Fast Dockerfile.fast"
time3=$(measure_build Dockerfile.fast fast | tail -1)

# Calculate improvements
echo "Summary"
echo "======="
echo "Minimal: ${time1}s"
echo "Ultra: ${time2}s"
echo "Fast: ${time3}s"

if [ "$time1" -ne "999999" ] && [ "$time2" -ne "999999" ]; then
    improvement=$(( (time1 - time2) * 100 / time1 ))
    echo ""
    echo "Ultra is ${improvement}% faster than Minimal"
fi

if [ "$time1" -ne "999999" ] && [ "$time3" -ne "999999" ]; then
    improvement=$(( (time1 - time3) * 100 / time1 ))
    echo "Fast is ${improvement}% faster than Minimal"
fi

# Show all images
echo ""
echo "Image sizes:"
docker images | grep test- | awk '{printf "%-20s %s\n", $1, $7}'

# Test cache performance
echo ""
echo "Testing cached rebuilds..."
echo "========================="

for df in Dockerfile.minimal Dockerfile.ultra Dockerfile.fast; do
    name=$(basename $df .Dockerfile)
    echo -n "$name (cached): "
    start=$(date +%s)
    if docker build -f $df -t test-${name}-cached . -q >/dev/null 2>&1; then
        end=$(date +%s)
        echo "$((end - start))s"
    else
        echo "Failed"
    fi
done

# Cleanup
echo ""
echo "Cleaning up test images..."
docker images | grep test- | awk '{print $3}' | xargs docker rmi -f 2>/dev/null || true