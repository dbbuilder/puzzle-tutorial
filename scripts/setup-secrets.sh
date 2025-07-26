#!/bin/bash

# Collaborative Puzzle Platform - Secrets Setup Script (Linux/Mac)
# This script helps developers set up required secrets for running the application

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT=""
USE_KEYVAULT=false
KEYVAULT_NAME=""
NON_INTERACTIVE=false

# Function to display usage
usage() {
    echo "Usage: $0 -e <environment> [-k <keyvault-name>] [-n]"
    echo "  -e: Environment (Development, Staging, Production)"
    echo "  -k: Azure Key Vault name (optional, uses .NET user secrets if not specified)"
    echo "  -n: Non-interactive mode (uses defaults)"
    echo ""
    echo "Examples:"
    echo "  $0 -e Development"
    echo "  $0 -e Production -k kv-puzzle-prod"
    exit 1
}

# Parse command line arguments
while getopts "e:k:nh" opt; do
    case $opt in
        e)
            ENVIRONMENT=$OPTARG
            ;;
        k)
            USE_KEYVAULT=true
            KEYVAULT_NAME=$OPTARG
            ;;
        n)
            NON_INTERACTIVE=true
            ;;
        h)
            usage
            ;;
        \?)
            echo "Invalid option: -$OPTARG" >&2
            usage
            ;;
    esac
done

# Validate environment
if [ -z "$ENVIRONMENT" ]; then
    echo -e "${RED}Error: Environment is required${NC}"
    usage
fi

if [[ ! "$ENVIRONMENT" =~ ^(Development|Staging|Production)$ ]]; then
    echo -e "${RED}Error: Invalid environment. Must be Development, Staging, or Production${NC}"
    usage
fi

# Function to prompt for secret value
get_secret_value() {
    local secret_name=$1
    local description=$2
    local default_value=$3
    local is_password=$4
    
    if [ "$NON_INTERACTIVE" = true ] && [ -n "$default_value" ]; then
        echo "$default_value"
        return
    fi
    
    echo -e "\n${CYAN}${description}${NC}"
    if [ -n "$default_value" ]; then
        echo -e "Default: ${default_value}"
    fi
    
    if [ "$is_password" = true ]; then
        read -s -p "$secret_name: " value
        echo
    else
        read -p "$secret_name: " value
    fi
    
    if [ -z "$value" ] && [ -n "$default_value" ]; then
        echo "$default_value"
    else
        echo "$value"
    fi
}

# Function to generate random password
generate_password() {
    local length=${1:-32}
    LC_ALL=C tr -dc 'A-Za-z0-9!@#$%^&*' < /dev/urandom | head -c "$length"
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Main script
echo -e "${GREEN}Collaborative Puzzle Platform - Secrets Setup${NC}"
echo -e "${GREEN}=============================================${NC}"
echo -e "${YELLOW}Environment: $ENVIRONMENT${NC}"

# Check prerequisites
if [ "$USE_KEYVAULT" = true ]; then
    if [ -z "$KEYVAULT_NAME" ]; then
        echo -e "${RED}Error: Key Vault name is required when using Azure Key Vault${NC}"
        exit 1
    fi
    
    # Check if Azure CLI is installed
    if ! command_exists az; then
        echo -e "${RED}Error: Azure CLI is not installed${NC}"
        echo "Please install it from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
        exit 1
    fi
    
    # Check if logged in to Azure
    if ! az account show >/dev/null 2>&1; then
        echo -e "${YELLOW}Please log in to Azure...${NC}"
        az login
    fi
    
    # Test Key Vault access
    echo -e "${YELLOW}Testing Key Vault access...${NC}"
    if ! az keyvault show --name "$KEYVAULT_NAME" >/dev/null 2>&1; then
        echo -e "${RED}Error: Cannot access Key Vault '$KEYVAULT_NAME'${NC}"
        echo "Please check the name and your permissions."
        exit 1
    fi
    
    echo -e "${GREEN}Successfully connected to Key Vault '$KEYVAULT_NAME'${NC}"
fi

# Check if dotnet is installed
if ! command_exists dotnet; then
    echo -e "${RED}Error: .NET SDK is not installed${NC}"
    echo "Please install it from https://dotnet.microsoft.com/download"
    exit 1
fi

# Define secrets to collect
declare -A secrets
declare -A secret_values

# Collect secret values
echo -e "\n${YELLOW}Please provide the following secret values:${NC}"

# Database
sql_server=$(get_secret_value "SqlServer" "SQL Server connection details" "Server=localhost;Database=CollaborativePuzzle;Integrated Security=true;TrustServerCertificate=true;" false)
sql_username=$(get_secret_value "SqlUsername" "SQL Server username (leave empty for integrated auth)" "" false)
sql_password=$(get_secret_value "SqlPassword" "SQL Server password" "" true)

# Build SQL connection string
if [ -n "$sql_username" ]; then
    sql_server=$(echo "$sql_server" | sed 's/Integrated Security=true;//g')
    sql_connection="${sql_server}User Id=${sql_username};Password=${sql_password};"
else
    sql_connection="$sql_server"
fi
secret_values["SqlConnectionString"]="$sql_connection"

# Redis
secret_values["RedisConnectionString"]=$(get_secret_value "RedisConnectionString" "Redis connection string" "localhost:6379" false)

# Azure Storage
storage_account=$(get_secret_value "StorageAccountName" "Azure Storage account name" "" false)
storage_key=$(get_secret_value "StorageAccountKey" "Azure Storage account key" "" true)

if [ -n "$storage_account" ] && [ -n "$storage_key" ]; then
    secret_values["StorageConnectionString"]="DefaultEndpointsProtocol=https;AccountName=${storage_account};AccountKey=${storage_key};EndpointSuffix=core.windows.net"
fi

# Application Insights
secret_values["AppInsightsInstrumentationKey"]=$(get_secret_value "AppInsightsInstrumentationKey" "Application Insights instrumentation key" "" false)

# Authentication
secret_values["AzureAdTenantId"]=$(get_secret_value "AzureAdTenantId" "Azure AD Tenant ID" "" false)
secret_values["AzureAdClientId"]=$(get_secret_value "AzureAdClientId" "Azure AD Client ID (App Registration)" "" false)
secret_values["AzureAdClientSecret"]=$(get_secret_value "AzureAdClientSecret" "Azure AD Client Secret" "" true)

# WebRTC TURN Server
secret_values["TurnServerUrl"]=$(get_secret_value "TurnServerUrl" "TURN server URL (e.g., turn:turnserver.example.com:3478)" "" false)
secret_values["TurnServerUsername"]=$(get_secret_value "TurnServerUsername" "TURN server username" "" false)
secret_values["TurnServerCredential"]=$(get_secret_value "TurnServerCredential" "TURN server credential/password" "" true)

# MQTT
secret_values["MqttBrokerHost"]=$(get_secret_value "MqttBrokerHost" "MQTT broker hostname" "localhost" false)
secret_values["MqttUsername"]=$(get_secret_value "MqttUsername" "MQTT username" "" false)
secret_values["MqttPassword"]=$(get_secret_value "MqttPassword" "MQTT password" "" true)

# JWT Signing Key
default_jwt_key=$(generate_password 64)
secret_values["JwtSigningKey"]=$(get_secret_value "JwtSigningKey" "JWT signing key (will be auto-generated if empty)" "$default_jwt_key" true)

# Store secrets
echo -e "\n${YELLOW}Storing secrets...${NC}"

if [ "$USE_KEYVAULT" = true ]; then
    # Store in Azure Key Vault
    for secret_name in "${!secret_values[@]}"; do
        value="${secret_values[$secret_name]}"
        if [ -n "$value" ]; then
            echo -e "Setting secret: $secret_name"
            az keyvault secret set \
                --vault-name "$KEYVAULT_NAME" \
                --name "$secret_name" \
                --value "$value" \
                --output none
        fi
    done
    
    echo -e "\n${GREEN}Secrets successfully stored in Azure Key Vault '$KEYVAULT_NAME'${NC}"
    echo -e "${YELLOW}Make sure your application has the 'Key Vault Secrets User' role assignment.${NC}"
    
else
    # Store in .NET user secrets
    PROJECT_PATH="../src/CollaborativePuzzle.Api"
    
    # Change to project directory
    cd "$(dirname "$0")/$PROJECT_PATH" || exit 1
    
    # Initialize user secrets if not already done
    if ! dotnet user-secrets list >/dev/null 2>&1; then
        echo -e "Initializing user secrets..."
        dotnet user-secrets init
    fi
    
    # Set each secret
    if [ -n "${secret_values[SqlConnectionString]}" ]; then
        dotnet user-secrets set "ConnectionStrings:DefaultConnection" "${secret_values[SqlConnectionString]}"
    fi
    
    if [ -n "${secret_values[RedisConnectionString]}" ]; then
        dotnet user-secrets set "ConnectionStrings:Redis" "${secret_values[RedisConnectionString]}"
    fi
    
    if [ -n "${secret_values[StorageConnectionString]}" ]; then
        dotnet user-secrets set "ConnectionStrings:AzureStorage" "${secret_values[StorageConnectionString]}"
    fi
    
    if [ -n "${secret_values[AppInsightsInstrumentationKey]}" ]; then
        dotnet user-secrets set "ApplicationInsights:InstrumentationKey" "${secret_values[AppInsightsInstrumentationKey]}"
    fi
    
    if [ -n "${secret_values[AzureAdTenantId]}" ]; then
        dotnet user-secrets set "Authentication:AzureAd:TenantId" "${secret_values[AzureAdTenantId]}"
    fi
    
    if [ -n "${secret_values[AzureAdClientId]}" ]; then
        dotnet user-secrets set "Authentication:AzureAd:ClientId" "${secret_values[AzureAdClientId]}"
    fi
    
    if [ -n "${secret_values[AzureAdClientSecret]}" ]; then
        dotnet user-secrets set "Authentication:AzureAd:ClientSecret" "${secret_values[AzureAdClientSecret]}"
    fi
    
    if [ -n "${secret_values[TurnServerUrl]}" ]; then
        dotnet user-secrets set "WebRTC:TurnServer:Url" "${secret_values[TurnServerUrl]}"
    fi
    
    if [ -n "${secret_values[TurnServerUsername]}" ]; then
        dotnet user-secrets set "WebRTC:TurnServer:Username" "${secret_values[TurnServerUsername]}"
    fi
    
    if [ -n "${secret_values[TurnServerCredential]}" ]; then
        dotnet user-secrets set "WebRTC:TurnServer:Credential" "${secret_values[TurnServerCredential]}"
    fi
    
    if [ -n "${secret_values[MqttBrokerHost]}" ]; then
        dotnet user-secrets set "MQTT:BrokerHost" "${secret_values[MqttBrokerHost]}"
    fi
    
    if [ -n "${secret_values[MqttUsername]}" ]; then
        dotnet user-secrets set "MQTT:Username" "${secret_values[MqttUsername]}"
    fi
    
    if [ -n "${secret_values[MqttPassword]}" ]; then
        dotnet user-secrets set "MQTT:Password" "${secret_values[MqttPassword]}"
    fi
    
    if [ -n "${secret_values[JwtSigningKey]}" ]; then
        dotnet user-secrets set "Authentication:JwtBearer:SigningKey" "${secret_values[JwtSigningKey]}"
    fi
    
    echo -e "\n${GREEN}Secrets successfully stored in .NET user secrets${NC}"
    echo -e "${YELLOW}User secrets are stored in:${NC}"
    echo -e "$HOME/.microsoft/usersecrets"
    
    cd - >/dev/null
fi

# Generate environment-specific configuration file
CONFIG_PATH="$(dirname "$0")/../src/CollaborativePuzzle.Api/appsettings.$ENVIRONMENT.json"

cat > "$CONFIG_PATH" <<EOF
{
  "Environment": "$ENVIRONMENT",
  "Logging": {
    "LogLevel": {
      "Default": "$([ "$ENVIRONMENT" = "Production" ] && echo "Warning" || echo "Information")",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "$([ "$ENVIRONMENT" = "Development" ] && echo "Information" || echo "Warning")"
    }
  }$([ "$USE_KEYVAULT" = true ] && echo ',
  "AzureKeyVault": {
    "VaultUri": "https://'$KEYVAULT_NAME'.vault.azure.net/"
  }' || echo '')
}
EOF

echo -e "\n${GREEN}Configuration file created at: $CONFIG_PATH${NC}"

# Create summary file
SUMMARY_PATH="$(dirname "$0")/secrets-summary-$ENVIRONMENT.txt"

cat > "$SUMMARY_PATH" <<EOF
Collaborative Puzzle Platform - Secrets Configuration Summary
============================================================
Generated: $(date)
Environment: $ENVIRONMENT
Storage: $([ "$USE_KEYVAULT" = true ] && echo "Azure Key Vault: $KEYVAULT_NAME" || echo ".NET User Secrets")

Configured Secrets:
$(for key in "${!secret_values[@]}"; do [ -n "${secret_values[$key]}" ] && echo "- $key"; done)

Next Steps:
1. Run database migrations: dotnet ef database update
2. Ensure Redis is running (Docker: docker run -d -p 6379:6379 redis:alpine)
3. Configure Azure resources if using cloud services
4. Run the application: dotnet run --environment $ENVIRONMENT

For production deployments:
- Ensure the application's managed identity has access to Key Vault
- Configure firewall rules for SQL Server and Redis
- Set up Application Insights for monitoring
- Configure CORS settings for your domain
EOF

echo -e "\n${GREEN}Setup complete! Summary saved to: $SUMMARY_PATH${NC}"
echo -e "This summary file is not tracked by git and contains no sensitive values."

# Make script executable
chmod +x "$0"