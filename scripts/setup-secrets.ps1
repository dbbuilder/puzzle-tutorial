<#
.SYNOPSIS
    Sets up secrets for the Collaborative Puzzle Platform in Azure Key Vault or local user secrets.

.DESCRIPTION
    This script helps developers set up the required secrets for running the application.
    It can store secrets in Azure Key Vault (for production) or in .NET user secrets (for development).

.PARAMETER Environment
    The environment to set up secrets for (Development, Staging, Production)

.PARAMETER UseKeyVault
    Whether to store secrets in Azure Key Vault (requires Azure CLI and appropriate permissions)

.PARAMETER KeyVaultName
    The name of the Azure Key Vault to use (required if UseKeyVault is specified)

.EXAMPLE
    .\setup-secrets.ps1 -Environment Development
    Sets up secrets for development using .NET user secrets

.EXAMPLE
    .\setup-secrets.ps1 -Environment Production -UseKeyVault -KeyVaultName "kv-puzzle-prod"
    Sets up secrets in Azure Key Vault for production
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,
    
    [switch]$UseKeyVault,
    
    [string]$KeyVaultName,
    
    [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"

# Function to prompt for secret value
function Get-SecretValue {
    param(
        [string]$SecretName,
        [string]$Description,
        [string]$DefaultValue = "",
        [bool]$IsPassword = $false
    )
    
    if ($NonInteractive -and $DefaultValue) {
        return $DefaultValue
    }
    
    Write-Host "`n$Description" -ForegroundColor Cyan
    if ($DefaultValue) {
        Write-Host "Default: $DefaultValue" -ForegroundColor DarkGray
    }
    
    if ($IsPassword) {
        $secureString = Read-Host -Prompt $SecretName -AsSecureString
        $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureString)
        return [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    } else {
        $value = Read-Host -Prompt $SecretName
        if ([string]::IsNullOrWhiteSpace($value) -and $DefaultValue) {
            return $DefaultValue
        }
        return $value
    }
}

# Function to generate a random password
function New-RandomPassword {
    param(
        [int]$Length = 32
    )
    
    $chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*"
    $password = ""
    $random = New-Object System.Random
    
    for ($i = 0; $i -lt $Length; $i++) {
        $password += $chars[$random.Next($chars.Length)]
    }
    
    return $password
}

# Function to test Azure Key Vault connectivity
function Test-KeyVaultAccess {
    param([string]$VaultName)
    
    try {
        $null = az keyvault show --name $VaultName 2>$null
        return $true
    } catch {
        return $false
    }
}

# Main script
Write-Host "Collaborative Puzzle Platform - Secrets Setup" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow

# Check prerequisites
if ($UseKeyVault) {
    if ([string]::IsNullOrWhiteSpace($KeyVaultName)) {
        Write-Error "KeyVaultName is required when using Azure Key Vault"
        exit 1
    }
    
    # Check if Azure CLI is installed
    try {
        $null = az --version
    } catch {
        Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecliwindows"
        exit 1
    }
    
    # Check if logged in to Azure
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "Please log in to Azure..." -ForegroundColor Yellow
        az login
    }
    
    # Test Key Vault access
    Write-Host "Testing Key Vault access..." -ForegroundColor Yellow
    if (-not (Test-KeyVaultAccess -VaultName $KeyVaultName)) {
        Write-Error "Cannot access Key Vault '$KeyVaultName'. Please check the name and your permissions."
        exit 1
    }
    
    Write-Host "Successfully connected to Key Vault '$KeyVaultName'" -ForegroundColor Green
}

# Define secrets to collect
$secrets = @{
    # Database
    "SqlServer" = @{
        Description = "SQL Server connection details"
        IsPassword = $false
        Default = "Server=localhost;Database=CollaborativePuzzle;Integrated Security=true;TrustServerCertificate=true;"
    }
    "SqlUsername" = @{
        Description = "SQL Server username (leave empty for integrated auth)"
        IsPassword = $false
        Default = ""
    }
    "SqlPassword" = @{
        Description = "SQL Server password"
        IsPassword = $true
        Default = ""
    }
    
    # Redis
    "RedisConnectionString" = @{
        Description = "Redis connection string"
        IsPassword = $false
        Default = "localhost:6379"
    }
    
    # Azure Storage
    "StorageAccountName" = @{
        Description = "Azure Storage account name"
        IsPassword = $false
        Default = ""
    }
    "StorageAccountKey" = @{
        Description = "Azure Storage account key"
        IsPassword = $true
        Default = ""
    }
    
    # Application Insights
    "AppInsightsInstrumentationKey" = @{
        Description = "Application Insights instrumentation key"
        IsPassword = $false
        Default = ""
    }
    
    # Authentication
    "AzureAdTenantId" = @{
        Description = "Azure AD Tenant ID"
        IsPassword = $false
        Default = ""
    }
    "AzureAdClientId" = @{
        Description = "Azure AD Client ID (App Registration)"
        IsPassword = $false
        Default = ""
    }
    "AzureAdClientSecret" = @{
        Description = "Azure AD Client Secret"
        IsPassword = $true
        Default = ""
    }
    
    # WebRTC TURN Server
    "TurnServerUrl" = @{
        Description = "TURN server URL (e.g., turn:turnserver.example.com:3478)"
        IsPassword = $false
        Default = ""
    }
    "TurnServerUsername" = @{
        Description = "TURN server username"
        IsPassword = $false
        Default = ""
    }
    "TurnServerCredential" = @{
        Description = "TURN server credential/password"
        IsPassword = $true
        Default = ""
    }
    
    # MQTT
    "MqttBrokerHost" = @{
        Description = "MQTT broker hostname"
        IsPassword = $false
        Default = "localhost"
    }
    "MqttUsername" = @{
        Description = "MQTT username"
        IsPassword = $false
        Default = ""
    }
    "MqttPassword" = @{
        Description = "MQTT password"
        IsPassword = $true
        Default = ""
    }
    
    # JWT Signing Key
    "JwtSigningKey" = @{
        Description = "JWT signing key (will be auto-generated if empty)"
        IsPassword = $true
        Default = (New-RandomPassword -Length 64)
    }
}

# Collect secret values
$secretValues = @{}
Write-Host "`nPlease provide the following secret values:" -ForegroundColor Yellow

foreach ($secretName in $secrets.Keys) {
    $secret = $secrets[$secretName]
    $value = Get-SecretValue `
        -SecretName $secretName `
        -Description $secret.Description `
        -DefaultValue $secret.Default `
        -IsPassword $secret.IsPassword
    
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        $secretValues[$secretName] = $value
    }
}

# Build connection strings
$sqlConnection = $secretValues["SqlServer"]
if ($secretValues["SqlUsername"]) {
    $sqlConnection = $sqlConnection -replace "Integrated Security=true;", ""
    $sqlConnection += "User Id=$($secretValues["SqlUsername"]);Password=$($secretValues["SqlPassword"]);"
}
$secretValues["SqlConnectionString"] = $sqlConnection

if ($secretValues["StorageAccountName"] -and $secretValues["StorageAccountKey"]) {
    $secretValues["StorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=$($secretValues["StorageAccountName"]);AccountKey=$($secretValues["StorageAccountKey"]);EndpointSuffix=core.windows.net"
}

# Store secrets
Write-Host "`nStoring secrets..." -ForegroundColor Yellow

if ($UseKeyVault) {
    # Store in Azure Key Vault
    foreach ($secretName in $secretValues.Keys) {
        $value = $secretValues[$secretName]
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            Write-Host "Setting secret: $secretName" -ForegroundColor Gray
            az keyvault secret set `
                --vault-name $KeyVaultName `
                --name $secretName `
                --value $value `
                --output none
        }
    }
    
    Write-Host "`nSecrets successfully stored in Azure Key Vault '$KeyVaultName'" -ForegroundColor Green
    Write-Host "Make sure your application has the 'Key Vault Secrets User' role assignment." -ForegroundColor Yellow
    
} else {
    # Store in .NET user secrets
    $projectPath = Join-Path $PSScriptRoot ".." "src" "CollaborativePuzzle.Api"
    
    # Initialize user secrets if not already done
    Push-Location $projectPath
    try {
        $userSecretsId = dotnet user-secrets list 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Initializing user secrets..." -ForegroundColor Gray
            dotnet user-secrets init
        }
        
        # Set each secret
        foreach ($secretName in $secretValues.Keys) {
            $value = $secretValues[$secretName]
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                Write-Host "Setting secret: $secretName" -ForegroundColor Gray
                
                # Map to configuration structure
                $configPath = switch -Wildcard ($secretName) {
                    "Sql*" { "ConnectionStrings:DefaultConnection" }
                    "Redis*" { "ConnectionStrings:Redis" }
                    "Storage*" { "ConnectionStrings:AzureStorage" }
                    "AppInsights*" { "ApplicationInsights:InstrumentationKey" }
                    "AzureAd*" { "Authentication:AzureAd:$secretName" }
                    "Turn*" { "WebRTC:TurnServer:${secretName -replace 'TurnServer', ''}" }
                    "Mqtt*" { "MQTT:${secretName -replace 'Mqtt', ''}" }
                    "Jwt*" { "Authentication:JwtBearer:SigningKey" }
                    default { $secretName }
                }
                
                if ($secretName -eq "SqlConnectionString") {
                    dotnet user-secrets set "ConnectionStrings:DefaultConnection" $value
                } elseif ($secretName -eq "StorageConnectionString") {
                    dotnet user-secrets set "ConnectionStrings:AzureStorage" $value
                } else {
                    dotnet user-secrets set $configPath $value
                }
            }
        }
        
        Write-Host "`nSecrets successfully stored in .NET user secrets" -ForegroundColor Green
        Write-Host "User secrets are stored in:" -ForegroundColor Yellow
        Write-Host "$env:APPDATA\Microsoft\UserSecrets" -ForegroundColor Gray
        
    } finally {
        Pop-Location
    }
}

# Generate environment-specific configuration file
$configPath = Join-Path $PSScriptRoot ".." "src" "CollaborativePuzzle.Api" "appsettings.$Environment.json"
$config = @{
    Environment = $Environment
    Logging = @{
        LogLevel = @{
            Default = if ($Environment -eq "Production") { "Warning" } else { "Information" }
            "Microsoft.AspNetCore" = "Warning"
            "Microsoft.EntityFrameworkCore" = if ($Environment -eq "Development") { "Information" } else { "Warning" }
        }
    }
}

if ($UseKeyVault) {
    $config.AzureKeyVault = @{
        VaultUri = "https://$KeyVaultName.vault.azure.net/"
    }
}

$configJson = $config | ConvertTo-Json -Depth 10
$configJson | Out-File -FilePath $configPath -Encoding utf8

Write-Host "`nConfiguration file created at: $configPath" -ForegroundColor Green

# Create a summary file (not tracked by git)
$summaryPath = Join-Path $PSScriptRoot "secrets-summary-$Environment.txt"
@"
Collaborative Puzzle Platform - Secrets Configuration Summary
============================================================
Generated: $(Get-Date)
Environment: $Environment
Storage: $(if ($UseKeyVault) { "Azure Key Vault: $KeyVaultName" } else { ".NET User Secrets" })

Configured Secrets:
$($secretValues.Keys | ForEach-Object { "- $_" } | Out-String)

Next Steps:
1. Run database migrations: dotnet ef database update
2. Ensure Redis is running (Docker: docker run -d -p 6379:6379 redis:alpine)
3. Configure Azure resources if using cloud services
4. Run the application: dotnet run --environment $Environment

For production deployments:
- Ensure the application's managed identity has access to Key Vault
- Configure firewall rules for SQL Server and Redis
- Set up Application Insights for monitoring
- Configure CORS settings for your domain
"@ | Out-File -FilePath $summaryPath -Encoding utf8

Write-Host "`nSetup complete! Summary saved to: $summaryPath" -ForegroundColor Green
Write-Host "This summary file is not tracked by git and contains no sensitive values." -ForegroundColor Gray