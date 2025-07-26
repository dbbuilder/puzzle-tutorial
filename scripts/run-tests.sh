#!/bin/bash

# Collaborative Puzzle Platform - Test Runner Script

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to run tests
run_test_suite() {
    local suite=$1
    local project=$2
    
    print_status "$BLUE" "\n🧪 Running $suite..."
    
    if dotnet test "$project" --logger "console;verbosity=normal" --collect:"XPlat Code Coverage"; then
        print_status "$GREEN" "✓ $suite passed"
    else
        print_status "$RED" "✗ $suite failed"
        return 1
    fi
}

# Main menu
show_menu() {
    echo -e "${BLUE}Collaborative Puzzle Platform - Test Runner${NC}"
    echo "==========================================="
    echo ""
    echo "Select test suite to run:"
    echo "1) Unit Tests"
    echo "2) Integration Tests (requires Docker)"
    echo "3) E2E Tests (requires running app)"
    echo "4) Performance Benchmarks"
    echo "5) Load Tests"
    echo "6) All Tests"
    echo "7) Code Coverage Report"
    echo "0) Exit"
    echo ""
}

# Check prerequisites
check_prerequisites() {
    local test_type=$1
    
    case $test_type in
        "integration")
            if ! docker info >/dev/null 2>&1; then
                print_status "$RED" "Docker is required for integration tests"
                return 1
            fi
            ;;
        "e2e")
            if ! curl -f http://localhost:5000/health >/dev/null 2>&1; then
                print_status "$RED" "Application must be running on http://localhost:5000 for E2E tests"
                return 1
            fi
            ;;
        "load")
            if ! curl -f http://localhost:5000/health >/dev/null 2>&1; then
                print_status "$RED" "Application must be running on http://localhost:5000 for load tests"
                return 1
            fi
            ;;
    esac
    
    return 0
}

# Unit tests
run_unit_tests() {
    run_test_suite "Unit Tests" "tests/CollaborativePuzzle.Tests/CollaborativePuzzle.Tests.csproj"
}

# Integration tests
run_integration_tests() {
    if check_prerequisites "integration"; then
        run_test_suite "Integration Tests" "tests/CollaborativePuzzle.IntegrationTests/CollaborativePuzzle.IntegrationTests.csproj"
    fi
}

# E2E tests
run_e2e_tests() {
    if check_prerequisites "e2e"; then
        print_status "$YELLOW" "Installing Playwright browsers..."
        cd tests/CollaborativePuzzle.E2ETests
        dotnet build
        pwsh bin/Debug/net8.0/playwright.ps1 install
        cd ../..
        
        run_test_suite "E2E Tests" "tests/CollaborativePuzzle.E2ETests/CollaborativePuzzle.E2ETests.csproj"
    fi
}

# Performance benchmarks
run_performance_tests() {
    print_status "$BLUE" "\n🚀 Running Performance Benchmarks..."
    
    cd tests/CollaborativePuzzle.PerformanceTests
    dotnet run -c Release -- $1
    cd ../..
}

# Load tests
run_load_tests() {
    if check_prerequisites "load"; then
        print_status "$BLUE" "\n📊 Running Load Tests..."
        
        cd tests/CollaborativePuzzle.LoadTests
        dotnet run -- http://localhost:5000 $1
        cd ../..
    fi
}

# Code coverage
generate_coverage_report() {
    print_status "$BLUE" "\n📈 Generating Code Coverage Report..."
    
    # Install ReportGenerator if not present
    if ! dotnet tool list -g | grep -q dotnet-reportgenerator-globaltool; then
        print_status "$YELLOW" "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    # Run tests with coverage
    dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./TestResults/
    
    # Generate HTML report
    reportgenerator \
        -reports:"**/TestResults/coverage.opencover.xml" \
        -targetdir:"TestResults/CoverageReport" \
        -reporttypes:"Html;Badges"
    
    print_status "$GREEN" "Coverage report generated at: TestResults/CoverageReport/index.html"
}

# Run all tests
run_all_tests() {
    local failed=0
    
    run_unit_tests || failed=$((failed + 1))
    run_integration_tests || failed=$((failed + 1))
    run_e2e_tests || failed=$((failed + 1))
    
    if [ $failed -eq 0 ]; then
        print_status "$GREEN" "\n✅ All tests passed!"
    else
        print_status "$RED" "\n❌ $failed test suite(s) failed"
    fi
    
    return $failed
}

# Main loop
while true; do
    show_menu
    read -p "Enter your choice: " choice
    
    case $choice in
        1) run_unit_tests ;;
        2) run_integration_tests ;;
        3) run_e2e_tests ;;
        4) 
            echo "Select benchmark:"
            echo "1) Redis"
            echo "2) SignalR"
            echo "3) All"
            read -p "Choice: " bench_choice
            case $bench_choice in
                1) run_performance_tests "redis" ;;
                2) run_performance_tests "signalr" ;;
                3) run_performance_tests ;;
            esac
            ;;
        5)
            echo "Select load test:"
            echo "1) SignalR"
            echo "2) API"
            echo "3) Stress"
            echo "4) All"
            read -p "Choice: " load_choice
            case $load_choice in
                1) run_load_tests "signalr" ;;
                2) run_load_tests "api" ;;
                3) run_load_tests "stress" ;;
                4) run_load_tests "all" ;;
            esac
            ;;
        6) run_all_tests ;;
        7) generate_coverage_report ;;
        0) 
            print_status "$GREEN" "Goodbye!"
            exit 0
            ;;
        *)
            print_status "$RED" "Invalid choice"
            ;;
    esac
    
    echo ""
    read -p "Press Enter to continue..."
done