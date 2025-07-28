# Development Standards: Naming and References

This document establishes standards to prevent common naming conflicts and non-specific references that have caused issues during development.

## Common Issues and Prevention Strategies

### 1. Duplicate Model Names (e.g., User Model Confusion)

**Issue**: Multiple models with the same name in different namespaces cause ambiguous references.
- `CollaborativePuzzle.Core.Models.User` (for DTOs/Authentication)
- `CollaborativePuzzle.Core.Entities.User` (for EF Core)

**Prevention**:
```csharp
// BAD: Ambiguous
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Core.Entities;
var user = new User(); // Which User?

// GOOD: Explicit
using AuthUser = CollaborativePuzzle.Core.Models.User;
using EntityUser = CollaborativePuzzle.Core.Entities.User;
// OR use fully qualified names
var authUser = new CollaborativePuzzle.Core.Models.User();
```

**Standard**: 
- Always use distinct names for models serving different purposes
- Prefix with purpose: `UserDto`, `UserEntity`, `UserViewModel`
- Document the purpose of each model in XML comments

### 2. Property Name Mismatches

**Issue**: Properties that seem like they should exist but don't, or have different names.
- `CurrentParticipants` vs `Participants.Count`
- `Width/Height` vs `ImageWidth/ImageHeight`
- `Difficulty` property missing from CreatePuzzleRequest

**Prevention**:
```csharp
// Document available properties
/// <summary>
/// Puzzle piece dimensions
/// </summary>
/// <remarks>
/// Use ImageWidth/ImageHeight for image dimensions
/// GridX/GridY for grid position
/// </remarks>
public class PuzzlePiece
{
    public int ImageWidth { get; set; }  // NOT Width
    public int ImageHeight { get; set; } // NOT Height
}
```

**Standard**:
- Always check entity definitions before assuming properties exist
- Use consistent naming patterns across similar entities
- Add XML comments highlighting non-obvious property names

### 3. Logger Type Issues

**Issue**: Using `ILogger<Program>` when Program class is internal.

**Prevention**:
```csharp
// BAD: Program is internal
ILogger<Program> logger

// GOOD: Use non-generic ILogger
ILogger logger

// BETTER: Create a marker class
public class ApiEndpoints { }
ILogger<ApiEndpoints> logger
```

**Standard**:
- Never use internal types as generic parameters for public APIs
- Prefer non-generic ILogger for minimal APIs
- Create public marker classes when type-specific logging is needed

### 4. Missing Interface Methods

**Issue**: Adding methods to interfaces without updating all implementations.

**Prevention**:
```csharp
// When adding to IRedisService interface:
// 1. Add to interface
// 2. Add to RedisService (real implementation)
// 3. Add to MinimalRedisService (test implementation)
// 4. Add to any mocks in tests

// Use IDE "Implement Interface" feature to ensure nothing is missed
```

**Standard**:
- Always use IDE refactoring tools when modifying interfaces
- Search for all implementations before changing interfaces
- Consider using default interface methods for optional functionality

### 5. Type Conversion Issues

**Issue**: Implicit conversions failing (decimal to double, arrays to IEnumerable).

**Prevention**:
```csharp
// BAD: Implicit conversion may fail
decimal percentComplete = 75.5m;
double progress = percentComplete; // Error

// GOOD: Explicit conversion
double progress = (double)percentComplete;

// BAD: Array conversion
IEnumerable<string> roles = result.Roles; // If Roles is string[]

// GOOD: Explicit
IEnumerable<string> roles = result.Roles.AsEnumerable();
```

**Standard**:
- Always use explicit casts for numeric type conversions
- Use `.ToArray()`, `.ToList()`, or `.AsEnumerable()` for collection conversions
- Be explicit about type conversions in method signatures

### 6. Missing Dependencies in Dependency Injection

**Issue**: Services used in endpoints not registered in DI container.

**Prevention**:
```csharp
// Document required services at the top of endpoint files
/// <summary>
/// Requires services:
/// - IUserService
/// - IJwtService
/// - ILogger
/// </summary>
public static class AuthEndpoints
```

**Standard**:
- Document all required services in XML comments
- Group related service registrations together in Program.cs
- Use extension methods to register related services

### 7. Nullable Reference Warnings

**Issue**: Possible null reference assignments without proper checks.

**Prevention**:
```csharp
// BAD: sortBy could be null
SortBy = sortBy,

// GOOD: Null coalescing
SortBy = sortBy ?? "created",

// BETTER: Use required properties or validation
public required string SortBy { get; init; } = "created";
```

**Standard**:
- Always handle nullable parameters with ?? operator
- Use `required` modifier for properties that must be set
- Enable nullable reference types and fix all warnings

## Naming Conventions

### DTOs and Models
- **Request DTOs**: `{Action}{Entity}Request` (e.g., `CreatePuzzleRequest`)
- **Response DTOs**: `{Entity}{Action}Response` (e.g., `LoginResponse`)
- **List Responses**: `{Entity}ListResponse` (e.g., `PuzzleListResponse`)
- **General DTOs**: `{Entity}Dto` (e.g., `UserDto`)
- **Entities**: Just the name (e.g., `User`, `Puzzle`)

### Services and Repositories
- **Interfaces**: `I{Name}` (e.g., `IUserService`)
- **Implementations**: `{Name}` (e.g., `UserService`)
- **Test Doubles**: `{Minimal|Mock|Fake}{Name}` (e.g., `MinimalRedisService`)

### API Endpoints
- **Endpoint Classes**: `{Entity}Endpoints` (e.g., `PuzzleEndpoints`)
- **V2 Helpers**: `{Entity}EndpointsV2` (e.g., `SessionEndpointsV2`)

## Pre-Development Checklist

Before implementing any feature:

1. **Check for existing models**:
   - Search for all classes with similar names
   - Verify which namespace/purpose each serves
   - Document if creating a new one is necessary

2. **Verify property names**:
   - Open the actual entity/model file
   - Don't assume property names based on convention
   - Check for computed properties vs stored properties

3. **Review interface contracts**:
   - Check all methods in the interface
   - Find all implementations that need updating
   - Consider backward compatibility

4. **Plan dependency injection**:
   - List all services the feature will need
   - Verify they're registered in Program.cs
   - Add registration if missing

5. **Consider nullability**:
   - Design with nullable reference types in mind
   - Use null coalescing for optional parameters
   - Validate required inputs early

## Quick Reference for Common Patterns

### When You See This Error | Do This

**CS0104: Ambiguous reference**
→ Use fully qualified names or type aliases

**CS1061: Does not contain a definition**
→ Check actual property names in the entity

**CS0535: Does not implement interface member**
→ Find all implementations and update them

**CS0051: Inconsistent accessibility**
→ Don't use internal types as generic parameters

**CS8601: Possible null reference**
→ Use ?? operator or add null check

**Missing service in DI**
→ Register in Program.cs before using

## Continuous Improvement

- Document new patterns as they're discovered
- Update this guide when new issues arise
- Share knowledge through code comments
- Use analyzers to enforce standards