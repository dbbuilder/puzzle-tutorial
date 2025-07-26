#!/bin/bash

# Kubernetes deployment script for Collaborative Puzzle Platform

set -e

ENVIRONMENT=${1:-dev}
NAMESPACE="puzzle-platform"
KUBECTL="kubectl"

if [ "$ENVIRONMENT" == "dev" ]; then
    NAMESPACE="puzzle-platform-dev"
fi

echo "Deploying to environment: $ENVIRONMENT"
echo "Namespace: $NAMESPACE"

# Build and apply manifests using Kustomize
echo "Building Kubernetes manifests..."
$KUBECTL apply -k overlays/$ENVIRONMENT

# Wait for namespace to be ready
echo "Waiting for namespace to be ready..."
$KUBECTL wait --for=condition=Active namespace/$NAMESPACE --timeout=30s || true

# Create namespace if it doesn't exist
$KUBECTL create namespace $NAMESPACE --dry-run=client -o yaml | $KUBECTL apply -f -

# Apply the configuration
echo "Applying configuration..."
$KUBECTL apply -k overlays/$ENVIRONMENT

# Wait for deployments
echo "Waiting for deployments to be ready..."
$KUBECTL -n $NAMESPACE wait --for=condition=available --timeout=300s deployment/puzzle-api
$KUBECTL -n $NAMESPACE wait --for=condition=ready --timeout=300s pod -l app=puzzle-redis
$KUBECTL -n $NAMESPACE wait --for=condition=ready --timeout=300s pod -l app=puzzle-db
$KUBECTL -n $NAMESPACE wait --for=condition=ready --timeout=300s pod -l app=puzzle-mqtt

# Show deployment status
echo "Deployment status:"
$KUBECTL -n $NAMESPACE get pods
$KUBECTL -n $NAMESPACE get services
$KUBECTL -n $NAMESPACE get ingress

echo "Deployment complete!"
echo ""
echo "To access the application:"
if [ "$ENVIRONMENT" == "dev" ]; then
    echo "  Add to /etc/hosts: 127.0.0.1 dev.puzzle.local"
    echo "  Port forward: kubectl -n $NAMESPACE port-forward svc/dev-puzzle-api-service 8080:80"
    echo "  Access at: http://dev.puzzle.local:8080"
else
    echo "  Production URL: https://puzzle.example.com"
fi