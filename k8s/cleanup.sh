#!/bin/bash

# Cleanup script for Kubernetes deployments

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Default values
ENVIRONMENT="dev"
NAMESPACE=""
FORCE=false

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
    echo "  -e, --env <environment>    Environment to clean up (dev/staging/production) [default: dev]"
    echo "  -n, --namespace <name>     Override namespace name"
    echo "  -f, --force               Skip confirmation prompt"
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
        -f|--force)
            FORCE=true
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

# Extra caution for production
if [ "$ENVIRONMENT" = "production" ] && [ "$FORCE" = false ]; then
    print_warn "WARNING: You are about to delete PRODUCTION resources!"
    print_warn "Namespace: $NAMESPACE"
    echo -n "Type 'DELETE PRODUCTION' to confirm: "
    read confirmation
    if [ "$confirmation" != "DELETE PRODUCTION" ]; then
        print_error "Cleanup cancelled."
        exit 1
    fi
elif [ "$FORCE" = false ]; then
    print_warn "This will delete all resources in namespace: $NAMESPACE"
    echo -n "Are you sure? (y/N): "
    read confirmation
    if [[ ! "$confirmation" =~ ^[Yy]$ ]]; then
        print_info "Cleanup cancelled."
        exit 0
    fi
fi

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    print_error "kubectl is not installed. Please install kubectl first."
    exit 1
fi

# Check if namespace exists
if ! kubectl get namespace "$NAMESPACE" &> /dev/null; then
    print_warn "Namespace $NAMESPACE does not exist."
    exit 0
fi

# Check if overlay directory exists
OVERLAY_DIR="k8s/overlays/$ENVIRONMENT"
if [ ! -d "$OVERLAY_DIR" ]; then
    print_warn "Overlay directory not found: $OVERLAY_DIR"
    print_info "Attempting to delete namespace directly..."
else
    # Delete resources using kustomize
    print_info "Deleting resources from $ENVIRONMENT environment..."
    kubectl delete -k "$OVERLAY_DIR" --ignore-not-found=true
fi

# Delete any remaining PVCs
print_info "Checking for persistent volume claims..."
PVCS=$(kubectl -n "$NAMESPACE" get pvc -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || true)
if [ -n "$PVCS" ]; then
    print_info "Deleting persistent volume claims..."
    kubectl -n "$NAMESPACE" delete pvc $PVCS
fi

# Option to delete the namespace entirely
if [ "$ENVIRONMENT" != "production" ]; then
    echo -n "Delete the entire namespace $NAMESPACE? (y/N): "
    read delete_ns
    if [[ "$delete_ns" =~ ^[Yy]$ ]]; then
        print_info "Deleting namespace: $NAMESPACE"
        kubectl delete namespace "$NAMESPACE"
    fi
fi

print_info "Cleanup completed for environment: $ENVIRONMENT"