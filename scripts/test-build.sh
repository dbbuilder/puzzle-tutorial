#!/bin/bash

echo "Testing minimal build locally..."

cd "$(dirname "$0")/.."

# Build the API project
dotnet build src/CollaborativePuzzle.Api/CollaborativePuzzle.Api.csproj -c Debug

# If build succeeds, try running it
if [ $? -eq 0 ]; then
    echo "Build succeeded! Starting API..."
    cd src/CollaborativePuzzle.Api
    dotnet run --no-build --urls "http://localhost:5000"
else
    echo "Build failed!"
    exit 1
fi