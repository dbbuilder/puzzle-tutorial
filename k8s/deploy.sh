#!/bin/bash

# Kubernetes deployment script for Collaborative Puzzle Platform

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="dev"
NAMESPACE=""
DRY_RUN=false
WAIT=true

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
usage() {
    echo "Usage: $0 [OPTIONS]"
    echo "Options:"
    echo "  -e, --env <environment>    Environment to deploy (dev/staging/production) [default: dev]"
    echo "  -n, --namespace <name>     Override namespace name"
    echo "  -d, --dry-run             Perform a dry run without applying changes"
    echo "  --no-wait                 Don't wait for deployments to be ready"
    echo "  -h, --help                Show this help message"
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--env)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -n|--namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        --no-wait)
            WAIT=false
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|production)$ ]]; then
    print_error "Invalid environment: $ENVIRONMENT"
    usage
fi

# Set namespace based on environment if not provided
if [ -z "$NAMESPACE" ]; then
    case $ENVIRONMENT in
        dev)
            NAMESPACE="puzzle-platform-dev"
            ;;
        staging)
            NAMESPACE="puzzle-platform-staging"
            ;;
        production)
            NAMESPACE="puzzle-platform-prod"
            ;;
    esac
fi

print_info "Deploying to environment: $ENVIRONMENT"
print_info "Namespace: $NAMESPACE"

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    print_error "kubectl is not installed. Please install kubectl first."
    exit 1
fi

# Check if we can connect to the cluster
if ! kubectl cluster-info &> /dev/null; then
    print_error "Cannot connect to Kubernetes cluster. Please check your kubeconfig."
    exit 1
fi

# Create namespace if it doesn't exist
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    print_info "Creating namespace: $NAMESPACE"
    if [ "$DRY_RUN" = false ]; then
        kubectl create namespace "$NAMESPACE"
    else
        echo "kubectl create namespace $NAMESPACE"
    fi
fi

# Check if overlay directory exists
OVERLAY_DIR="k8s/overlays/$ENVIRONMENT"
if [ ! -d "$OVERLAY_DIR" ]; then
    print_error "Overlay directory not found: $OVERLAY_DIR"
    exit 1
fi

# Build and validate manifests
print_info "Building Kubernetes manifests..."
if [ "$DRY_RUN" = true ]; then
    kubectl kustomize "$OVERLAY_DIR"
    print_info "Dry run completed. No changes were applied."
    exit 0
fi

# Apply manifests
print_info "Applying manifests..."
kubectl apply -k "$OVERLAY_DIR"

if [ "$WAIT" = true ]; then
    # Wait for deployments to be ready
    print_info "Waiting for deployments to be ready..."
    
    # Get all deployments in the namespace
    DEPLOYMENTS=$(kubectl -n "$NAMESPACE" get deployments -o jsonpath='{.items[*].metadata.name}')
    
    for deployment in $DEPLOYMENTS; do
        print_info "Waiting for deployment: $deployment"
        kubectl -n "$NAMESPACE" rollout status deployment/"$deployment" --timeout=300s
    done
    
    # Check if StatefulSets exist and wait for them
    if kubectl -n "$NAMESPACE" get statefulsets &> /dev/null; then
        STATEFULSETS=$(kubectl -n "$NAMESPACE" get statefulsets -o jsonpath='{.items[*].metadata.name}')
        for statefulset in $STATEFULSETS; do
            print_info "Waiting for statefulset: $statefulset"
            kubectl -n "$NAMESPACE" rollout status statefulset/"$statefulset" --timeout=300s
        done
    fi
fi

# Show deployment status
print_info "Deployment status:"
kubectl -n "$NAMESPACE" get pods
echo ""
kubectl -n "$NAMESPACE" get svc
echo ""
kubectl -n "$NAMESPACE" get ingress

# Show how to access the application
if [ "$ENVIRONMENT" = "dev" ]; then
    print_info "To access the application locally, you can use port-forward:"
    echo "  kubectl -n $NAMESPACE port-forward svc/puzzle-api 8080:80"
    echo "  Then access: http://localhost:8080"
fi

print_info "Deployment completed successfully!"

# Show logs command
print_info "To view logs:"
echo "  kubectl -n $NAMESPACE logs -f deployment/puzzle-api"