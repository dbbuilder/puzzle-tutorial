# Enhanced API Key Authentication Features

This document describes the enhanced API key authentication features implemented in Phase 3 of the Collaborative Puzzle Platform.

## Overview

The enhanced API key authentication system provides enterprise-grade features for managing programmatic access to the platform's APIs. These features include key rotation, hierarchical scope validation, usage analytics, and rate limiting integration.

## Key Features Implemented

### 1. API Key Rotation

API keys can now be rotated to maintain security without disrupting service:

```csharp
// Rotate an existing API key
POST /api/apikey/{keyId}/rotate
{
  "reason": "Regular security rotation"
}

// Response includes new key value (only shown once)
{
  "id": "new-key-id",
  "key": "cp_new_secure_key_value",
  "name": "Production API Key (Rotated)",
  "scopes": ["read_puzzles", "write_puzzles"],
  "rotatedFromKeyId": "old-key-id"
}
```

**Benefits:**
- Maintains same permissions and scopes
- Automatically revokes old key
- Provides audit trail with rotation history
- Zero-downtime key replacement

### 2. Hierarchical Scope Validation

Scopes now support hierarchical permissions with wildcards:

```csharp
// Scope hierarchy
"puzzles:*"     -> Grants all puzzle permissions
"sessions:*"    -> Grants all session permissions  
"admin:*"       -> Grants all admin permissions

// Example validation
ApiKey with scope "puzzles:*" satisfies requirements for:
- "read_puzzles"
- "write_puzzles" 
- "delete_puzzles"
```

**Available Hierarchical Scopes:**
- `puzzles:read`, `puzzles:write`, `puzzles:delete`, `puzzles:*`
- `sessions:read`, `sessions:write`, `sessions:*`
- `admin:users:*`, `admin:system:*`, `admin:*`

### 3. API Key Usage Analytics

Track detailed usage metrics for each API key:

```csharp
// Get usage statistics
GET /api/apikey/{keyId}/usage

// Response
{
  "totalRequests": 15420,
  "endpointUsage": {
    "/api/v1/puzzles": 8200,
    "/api/v1/sessions": 5100,
    "/api/v1/users": 2120
  },
  "statusCodeDistribution": {
    "200": 14500,
    "400": 600,
    "429": 320
  },
  "averageResponseTimeMs": 125.4,
  "firstUsed": "2024-01-15T10:00:00Z",
  "lastUsed": "2024-01-28T15:30:00Z"
}
```

**Metrics Tracked:**
- Total request count
- Per-endpoint usage
- Status code distribution
- Response time averages
- First/last usage timestamps

### 4. Rate Limiting Integration

API keys now have tiered rate limits:

```csharp
// Rate limit tiers
"basic"     -> 10 req/min, 100 req/hour, 1000 req/day
"standard"  -> 30 req/min, 500 req/hour, 5000 req/day
"premium"   -> 100 req/min, 1000 req/hour, 10000 req/day
"unlimited" -> No limits

// Custom limits per key
{
  "rateLimitTier": "custom",
  "maxRequestsPerMinute": 50,
  "maxRequestsPerHour": 750,
  "maxRequestsPerDay": 7500
}
```

**Features:**
- Per-key rate limiting
- Grace period warnings near expiration
- Custom rate limits override tier defaults
- Rate limit info returned in validation response

### 5. Enhanced Validation Response

API key validation now returns comprehensive information:

```csharp
// Enhanced validation response
{
  "isValid": true,
  "userId": "user123",
  "apiKeyId": "key456",
  "scopes": ["read_puzzles", "write_puzzles"],
  "rateLimitInfo": {
    "tier": "standard",
    "limit": 30,
    "currentUsage": 12,
    "window": "00:01:00",
    "resetsAt": "2024-01-28T15:32:00Z"
  },
  "isNearExpiration": true,
  "daysUntilExpiration": 3
}
```

### 6. API Key Templates

Create API keys from predefined templates:

```csharp
// Available templates
- "basic-read": Read-only access to puzzles and sessions
- "standard-full": Full access to puzzles and sessions
- "admin-all": Full administrative access

// Create from template
var apiKey = await apiKeyService.CreateApiKeyFromTemplateAsync(
    userId, 
    "My API Key", 
    "standard-full");
```

## Implementation Details

### Architecture

1. **Service Layer** (`ApiKeyService`)
   - Handles key generation, validation, rotation
   - Manages hierarchical scope validation
   - Tracks usage metrics via Redis

2. **Repository Layer** (`IApiKeyRepository`)
   - Data persistence for API keys
   - Template management
   - Audit trail storage

3. **Middleware** (`ApiKeyAuthenticationMiddleware`, `ApiKeyUsageTrackingMiddleware`)
   - Validates API keys on requests
   - Tracks usage metrics
   - Integrates with rate limiting

4. **Controllers** (`ApiKeyController`)
   - REST API endpoints
   - Key management operations
   - Usage statistics retrieval

### Security Considerations

1. **Key Storage**
   - Keys are hashed using SHA256 before storage
   - Original key value only returned on creation
   - No way to retrieve original key after creation

2. **Rotation Security**
   - Old keys immediately revoked on rotation
   - Rotation history maintained for audit
   - Grace period configurable for migration

3. **Rate Limiting**
   - Distributed rate limiting via Redis
   - Fails open if Redis unavailable
   - Per-key custom limits supported

### Performance Optimizations

1. **Caching**
   - Validation results cached for 5 minutes
   - Redis-based distributed cache
   - Automatic cache invalidation on changes

2. **Async Operations**
   - All database operations async
   - Fire-and-forget for usage tracking
   - Bulk operations for analytics

3. **Minimal Overhead**
   - Lightweight middleware implementation
   - Efficient Redis data structures
   - Optimized for high-throughput scenarios

## Usage Examples

### Creating an API Key with Custom Settings

```csharp
var request = new CreateApiKeyRequest
{
    Name = "Production API Key",
    Scopes = new[] { "puzzles:*", "sessions:read" },
    ExpiresInDays = 90,
    RateLimitTier = "premium",
    Metadata = new Dictionary<string, object>
    {
        ["environment"] = "production",
        ["team"] = "mobile-app"
    }
};

var response = await apiKeyController.CreateApiKey(request);
```

### Validating Hierarchical Scopes

```csharp
// Check if API key has required permissions
var hasAccess = apiKeyService.ValidateHierarchicalScopes(
    apiKey, 
    new[] { "read_puzzles", "write_puzzles" });

// "puzzles:*" scope would satisfy both requirements
```

### Tracking Custom Metrics

```csharp
// Track API key usage with custom metrics
await apiKeyService.TrackApiKeyUsageAsync(
    apiKey,
    "/api/v2/puzzles/search",
    200,
    145); // response time in ms
```

## Testing

Comprehensive test coverage includes:

- Unit tests for all service methods
- Integration tests for middleware
- Load tests for rate limiting
- Security tests for key rotation

See `ApiKeyServiceEnhancedTests.cs` for test examples.

## Future Enhancements

1. **OAuth2 Integration**
   - API keys as OAuth2 client credentials
   - Refresh token support
   - Standard OAuth2 flows

2. **Advanced Analytics**
   - Real-time usage dashboards
   - Anomaly detection
   - Cost allocation per key

3. **Key Policies**
   - IP address restrictions
   - Time-based access
   - Conditional permissions

4. **Webhook Integration**
   - Key expiration notifications
   - Usage threshold alerts
   - Security event webhooks