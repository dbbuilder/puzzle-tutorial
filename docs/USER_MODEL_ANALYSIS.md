# User Model Analysis and Recommendations

## Current Situation

The codebase currently has two User models with different purposes:

### 1. Core.Models.User (Authentication Model)
Location: `/src/CollaborativePuzzle.Core/Models/User.cs`

**Purpose**: Used for authentication and identity management
**Key Features**:
- Simple structure focused on authentication
- Contains PasswordHash and PasswordSalt for local authentication
- Has UserRoles navigation property for authorization
- String-based Id (suitable for various identity providers)

**Usage**:
- Used by IUserService for authentication
- Referenced in ApplicationDbContext for identity tables
- Used in JWT token generation
- Part of authentication DTOs and results

### 2. Core.Entities.User (Domain Entity)
Location: `/src/CollaborativePuzzle.Core/Entities/User.cs`

**Purpose**: Rich domain entity for the puzzle platform
**Key Features**:
- Comprehensive user profile (DisplayName, Avatar, Language preferences)
- External authentication support (Provider, ExternalId)
- User statistics (puzzles created/completed, active time)
- User preferences (voice chat, notifications)
- Multiple navigation properties for domain relationships
- Guid-based Id

**Usage**:
- Used in domain operations (puzzles, sessions, participants)
- Referenced in PuzzleDbContext for domain tables
- Contains business logic and domain-specific data

## Analysis

### Problems with Current Approach

1. **Duplication of Core Fields**: Username, Email, IsActive, CreatedAt exist in both models
2. **Synchronization Issues**: Changes to user data must be kept in sync between models
3. **Confusion**: Developers must understand which model to use in different contexts
4. **Data Integrity**: Risk of data inconsistency between the two representations

### Benefits of Current Approach

1. **Separation of Concerns**: Authentication is cleanly separated from domain logic
2. **Flexibility**: Each model can evolve independently
3. **Security**: Authentication data is isolated from domain operations
4. **Performance**: Lighter authentication model for frequent auth checks

## Recommended Solution: Unified Model with Separation

### Option 1: Single User Entity with Interfaces (RECOMMENDED)

Merge the models into a single entity that implements multiple interfaces:

```csharp
// Core/Entities/User.cs
public class User : IAuthenticationUser, IDomainUser
{
    // Common properties
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    
    // Authentication properties (IAuthenticationUser)
    public byte[] PasswordHash { get; set; }
    public byte[] PasswordSalt { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
    
    // Domain properties (IDomainUser)
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    // ... other domain properties
}

// Core/Interfaces/IAuthenticationUser.cs
public interface IAuthenticationUser
{
    Guid Id { get; }
    string Username { get; }
    string Email { get; }
    byte[] PasswordHash { get; }
    byte[] PasswordSalt { get; }
    ICollection<UserRole> UserRoles { get; }
}
```

**Benefits**:
- Single source of truth
- Type safety through interfaces
- Can still separate concerns via interfaces
- No synchronization needed

### Option 2: Keep Separate with Reference (Alternative)

Keep both models but have the domain entity reference the authentication entity:

```csharp
// Core/Entities/User.cs
public class User
{
    public Guid Id { get; set; }
    
    // Reference to auth user
    public string AuthUserId { get; set; }
    public virtual Core.Models.User AuthUser { get; set; }
    
    // Domain-specific properties only
    public string? DisplayName { get; set; }
    // ... other domain properties
}
```

**Benefits**:
- Clear separation maintained
- Can have different ID types
- Auth system remains independent

**Drawbacks**:
- Extra join for common operations
- More complex queries
- Still some duplication (Username, Email)

## Migration Strategy

### Phase 1: Create Unified Model
1. Create new unified User entity with all properties
2. Implement IAuthenticationUser interface
3. Update repositories to use new model

### Phase 2: Update Services
1. Update UserService to use unified model
2. Update authentication middleware
3. Update JWT generation

### Phase 3: Migrate Data
1. Create migration to merge user tables
2. Map existing data to unified structure
3. Update foreign key relationships

### Phase 4: Remove Old Models
1. Remove Core.Models.User
2. Update all references
3. Clean up unused code

## Implementation Plan

```csharp
// 1. Create unified entity
namespace CollaborativePuzzle.Core.Entities;

public class User : IAuthenticationUser, IAuditable
{
    // Identity
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // Authentication
    public byte[]? PasswordHash { get; set; } // Nullable for external auth
    public byte[]? PasswordSalt { get; set; }
    public string? ExternalId { get; set; }
    public string? Provider { get; set; }
    
    // Profile
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    
    // Preferences
    public UserPreferences Preferences { get; set; } = new();
    
    // Statistics
    public UserStatistics Statistics { get; set; } = new();
    
    // Navigation
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<Puzzle> CreatedPuzzles { get; set; } = new List<Puzzle>();
    // ... other navigations
}

// 2. Value objects for organization
public class UserPreferences
{
    public bool AllowVoiceChat { get; set; } = true;
    public bool AllowNotifications { get; set; } = true;
    public string PreferredLanguage { get; set; } = "en";
}

public class UserStatistics
{
    public int TotalPuzzlesCreated { get; set; }
    public int TotalPuzzlesCompleted { get; set; }
    public int TotalSessionsJoined { get; set; }
    public TimeSpan TotalActiveTime { get; set; } = TimeSpan.Zero;
}
```

## Decision Criteria

Choose **Option 1 (Unified Model)** if:
- You want a simpler architecture ✓
- You prefer single source of truth ✓
- You don't need complete isolation between auth and domain ✓
- You want better performance (no joins) ✓

Choose **Option 2 (Separate with Reference)** if:
- You need complete auth system isolation
- You plan to extract auth to a separate service
- You have different ID type requirements
- You want to support multiple auth providers with different user models

## Recommendation

**Implement Option 1 (Unified Model with Interfaces)** because:

1. **Simplicity**: One model is easier to maintain than two synchronized models
2. **Performance**: No joins needed for common operations
3. **Consistency**: Single source of truth eliminates sync issues
4. **Flexibility**: Interfaces provide the separation where needed
5. **Pragmatic**: The current separation seems over-engineered for the project's needs

The unified model with interfaces provides the best balance of simplicity, maintainability, and flexibility for this application.