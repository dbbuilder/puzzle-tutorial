# Secrets Management Guide

## Overview

The Collaborative Puzzle Platform uses a comprehensive secrets management system that supports both local development and production deployments. This guide explains how to manage secrets securely across different environments.

## Quick Start

### Development Environment

For local development, use the provided setup script:

**Windows (PowerShell):**
```powershell
.\scripts\setup-secrets.ps1 -Environment Development
```

**Linux/Mac:**
```bash
./scripts/setup-secrets.sh -e Development
```

This will prompt you for required secrets and store them in .NET user secrets.

### Production Environment

For production, use Azure Key Vault:

**Windows (PowerShell):**
```powershell
.\scripts\setup-secrets.ps1 -Environment Production -UseKeyVault -KeyVaultName "kv-puzzle-prod"
```

**Linux/Mac:**
```bash
./scripts/setup-secrets.sh -e Production -k kv-puzzle-prod
```

## Secret Categories

### 1. Database Secrets
- **SqlConnectionString**: Connection string for SQL Server
- **SqlUsername**: Database username (optional if using integrated auth)
- **SqlPassword**: Database password

### 2. Caching Secrets
- **RedisConnectionString**: Connection string for Redis cache

### 3. Storage Secrets
- **StorageAccountName**: Azure Storage account name
- **StorageAccountKey**: Azure Storage account key

### 4. Monitoring Secrets
- **AppInsightsInstrumentationKey**: Application Insights key

### 5. Authentication Secrets
- **AzureAdTenantId**: Azure AD tenant ID
- **AzureAdClientId**: Azure AD application ID
- **AzureAdClientSecret**: Azure AD client secret
- **JwtSigningKey**: Key for signing JWT tokens

### 6. Real-time Communication Secrets
- **TurnServerUrl**: WebRTC TURN server URL
- **TurnServerUsername**: TURN server username
- **TurnServerCredential**: TURN server password
- **MqttBrokerHost**: MQTT broker hostname
- **MqttUsername**: MQTT username
- **MqttPassword**: MQTT password

## Storage Mechanisms

### Development: .NET User Secrets

User secrets are stored outside the project directory to prevent accidental commits:

- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **Linux/Mac**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

Access in code:
```csharp
var connectionString = configuration["ConnectionStrings:DefaultConnection"];
```

### Production: Azure Key Vault

Secrets are stored in Azure Key Vault with managed identity access:

1. **Create Key Vault**:
   ```bash
   az keyvault create --name kv-puzzle-prod --resource-group rg-puzzle --location eastus
   ```

2. **Grant Access**:
   ```bash
   az keyvault set-policy --name kv-puzzle-prod \
     --object-id <managed-identity-object-id> \
     --secret-permissions get list
   ```

3. **Configure Application**:
   ```json
   {
     "AzureKeyVault": {
       "VaultUri": "https://kv-puzzle-prod.vault.azure.net/"
     }
   }
   ```

## Security Best Practices

### 1. Never Commit Secrets
- All `appsettings.*.json` files (except Example) are gitignored
- Use the provided `appsettings.Example.json` as a template
- Review commits for accidental secret exposure

### 2. Principle of Least Privilege
- Grant minimum required permissions
- Use managed identities where possible
- Rotate secrets regularly

### 3. Secret Rotation
```powershell
# Rotate a specific secret
az keyvault secret set --vault-name kv-puzzle-prod \
  --name SqlPassword --value "NewSecurePassword"
```

### 4. Audit Access
```bash
# View Key Vault access logs
az monitor activity-log list --resource-id \
  "/subscriptions/{sub-id}/resourceGroups/rg-puzzle/providers/Microsoft.KeyVault/vaults/kv-puzzle-prod"
```

## Environment-Specific Configuration

### Development
```json
{
  "Environment": "Development",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

### Production
```json
{
  "Environment": "Production",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AzureKeyVault": {
    "VaultUri": "https://kv-puzzle-prod.vault.azure.net/"
  }
}
```

## Troubleshooting

### Issue: Cannot access Key Vault
```bash
# Check current Azure account
az account show

# List Key Vault policies
az keyvault show --name kv-puzzle-prod --query "properties.accessPolicies"
```

### Issue: User secrets not working
```bash
# List current user secrets
cd src/CollaborativePuzzle.Api
dotnet user-secrets list

# Clear all user secrets
dotnet user-secrets clear
```

### Issue: Connection string format
For WSL connecting to Windows SQL Server:
```
Server=172.31.208.1,14333;Database=CollaborativePuzzle;User Id=sa;Password=YourPassword;TrustServerCertificate=true;
```

## CI/CD Integration

### GitHub Actions
```yaml
- name: Azure Login
  uses: azure/login@v1
  with:
    creds: ${{ secrets.AZURE_CREDENTIALS }}

- name: Get secrets from Key Vault
  uses: Azure/get-keyvault-secrets@v1
  with:
    keyvault: "kv-puzzle-prod"
    secrets: 'SqlConnectionString, RedisConnectionString'
```

### Azure DevOps
```yaml
- task: AzureKeyVault@2
  inputs:
    azureSubscription: 'Production-ServiceConnection'
    KeyVaultName: 'kv-puzzle-prod'
    SecretsFilter: '*'
```

## Manual Secret Configuration

If you prefer to configure secrets manually:

1. **Create secrets.json** (Development only):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=CollaborativePuzzle;...",
       "Redis": "localhost:6379"
     }
   }
   ```

2. **Set environment variables** (Alternative method):
   ```bash
   export ConnectionStrings__DefaultConnection="Server=..."
   export ConnectionStrings__Redis="localhost:6379"
   ```

3. **Use Azure App Configuration** (Enterprise):
   ```csharp
   builder.Configuration.AddAzureAppConfiguration(options =>
   {
       options.Connect(connectionString)
              .ConfigureKeyVault(kv => kv.SetCredential(new DefaultAzureCredential()));
   });
   ```

## Summary

- Use setup scripts for automated configuration
- Store development secrets in .NET user secrets
- Store production secrets in Azure Key Vault
- Never commit actual secret values
- Follow security best practices
- Rotate secrets regularly
- Monitor access logs