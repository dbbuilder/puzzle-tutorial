# Test-Driven Development (TDD) Guide

## Overview

This project follows Test-Driven Development (TDD) principles to ensure high code quality, maintainability, and comprehensive test coverage. This guide explains our TDD approach and best practices.

## TDD Cycle

We follow the classic Red-Green-Refactor cycle:

1. **Red**: Write a failing test that defines desired functionality
2. **Green**: Write minimal code to make the test pass
3. **Refactor**: Improve the code while keeping tests green

## Test Structure

### Test Organization

```
tests/
├── CollaborativePuzzle.Tests/          # Unit tests
│   ├── TestBase/                       # Base classes and infrastructure
│   ├── Helpers/                        # Test utilities and builders
│   ├── Core/                          # Core domain tests
│   │   ├── Entities/
│   │   ├── Services/
│   │   └── Validators/
│   ├── Infrastructure/                 # Infrastructure tests
│   │   ├── Repositories/
│   │   └── Services/
│   └── Api/                           # API endpoint tests
│       ├── Controllers/
│       └── Middleware/
└── CollaborativePuzzle.IntegrationTests/  # Integration tests
```

### Test Naming Convention

Tests follow the pattern: `MethodName_Scenario_ExpectedBehavior`

```csharp
[Fact]
public async Task CreatePuzzleAsync_WithValidData_ShouldCreatePuzzleSuccessfully()
{
    // Test implementation
}

[Fact]
public async Task DeletePuzzleAsync_WhenUserIsNotOwner_ShouldThrowUnauthorizedException()
{
    // Test implementation
}
```

## Writing Tests First

### Example: Creating a New Service

1. **Start with the test** (PuzzleServiceTests.cs):

```csharp
[Fact]
public async Task CreatePuzzleAsync_WithValidData_ShouldCreatePuzzleSuccessfully()
{
    // Arrange
    var userId = Guid.NewGuid();
    var puzzleTitle = "Test Puzzle";
    var imageData = new byte[] { 1, 2, 3, 4, 5 };
    
    // Define expected behavior
    _blobStorageMock
        .Setup(x => x.UploadImageAsync(It.IsAny<string>(), imageData, It.IsAny<string>()))
        .ReturnsAsync("https://storage.example.com/puzzle.jpg");
    
    // Act
    var result = await _sut.CreatePuzzleAsync(userId, puzzleTitle, imageData);
    
    // Assert
    result.Should().NotBeNull();
    result.Title.Should().Be(puzzleTitle);
    result.CreatedByUserId.Should().Be(userId);
}
```

2. **Create the interface** (IPuzzleService.cs):

```csharp
public interface IPuzzleService
{
    Task<Puzzle> CreatePuzzleAsync(Guid userId, string title, byte[] imageData);
}
```

3. **Implement minimal code** (PuzzleService.cs):

```csharp
public class PuzzleService : IPuzzleService
{
    public async Task<Puzzle> CreatePuzzleAsync(Guid userId, string title, byte[] imageData)
    {
        // Minimal implementation to make test pass
        var imageUrl = await _blobStorage.UploadImageAsync("puzzles", imageData, "image/jpeg");
        
        var puzzle = new Puzzle
        {
            Title = title,
            CreatedByUserId = userId,
            ImageUrl = imageUrl
        };
        
        return await _puzzleRepository.CreatePuzzleAsync(puzzle);
    }
}
```

4. **Refactor** once test passes:
   - Extract magic strings to constants
   - Add validation
   - Improve error handling
   - Add logging

## Test Patterns

### 1. Arrange-Act-Assert (AAA)

```csharp
[Fact]
public async Task TestMethod()
{
    // Arrange - Set up test data and mocks
    var testData = TestDataBuilder.Puzzle().Build();
    _mockService.Setup(x => x.Method()).ReturnsAsync(testData);
    
    // Act - Execute the method under test
    var result = await _sut.MethodUnderTest();
    
    // Assert - Verify the outcome
    result.Should().NotBeNull();
    result.Should().BeEquivalentTo(testData);
}
```

### 2. Test Data Builders

Use fluent builders for creating test data:

```csharp
var puzzle = TestDataBuilder.Puzzle()
    .WithTitle("Test Puzzle")
    .WithPieceCount(100)
    .WithDifficulty(PuzzleDifficulty.Medium)
    .WithCreator(userId)
    .Build();
```

### 3. Mock Verification

Always verify mock interactions:

```csharp
// Verify method was called with specific parameters
_mockRepository.Verify(x => x.SaveAsync(
    It.Is<Puzzle>(p => p.Title == "Expected Title")
), Times.Once);

// Verify method was never called
_mockService.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
```

## Integration Testing

### Using TestContainers

Integration tests use real databases via TestContainers:

```csharp
public class PuzzleApiIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task CreatePuzzle_ShouldPersistToDatabase()
    {
        // Arrange
        var request = new CreatePuzzleRequest { Title = "Test" };
        
        // Act
        var response = await Client.PostAsJsonAsync("/api/puzzles", request);
        
        // Assert
        response.Should().BeSuccessful();
        
        // Verify in database
        var puzzle = await DbContext.Puzzles.FirstOrDefaultAsync();
        puzzle.Should().NotBeNull();
        puzzle.Title.Should().Be("Test");
    }
}
```

### Test Isolation

Each test runs in isolation:
- Separate database transactions
- Clean state between tests
- No shared mutable state

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/CollaborativePuzzle.Tests

# Run tests matching pattern
dotnet test --filter "FullyQualifiedName~PuzzleService"

# Run tests in specific category
dotnet test --filter "Category=Unit"
```

### Visual Studio

- Test Explorer: `Ctrl+E, T`
- Run all tests: `Ctrl+R, A`
- Debug test: `Ctrl+R, Ctrl+T`
- Run tests in context: `Ctrl+R, T`

### Continuous Integration

Tests run automatically on:
- Pull requests
- Commits to main branch
- Nightly builds

## Best Practices

### 1. Test One Thing

Each test should verify a single behavior:

```csharp
// Good: Single assertion
[Fact]
public void CalculatePrice_WithValidInput_ReturnsCorrectPrice() { }

// Bad: Multiple behaviors
[Fact]
public void CalculatePrice_TestsEverything() { }
```

### 2. Independent Tests

Tests should not depend on:
- Other tests
- Test execution order
- External state

### 3. Fast Tests

- Mock external dependencies
- Use in-memory databases for unit tests
- Reserve TestContainers for integration tests

### 4. Readable Tests

Tests serve as documentation:

```csharp
[Fact]
public async Task JoinSession_WhenSessionIsFull_ShouldReturnSessionFullError()
{
    // Clear test intent from method name
    // Arrange section clearly shows preconditions
    // Act section shows what's being tested
    // Assert section shows expected outcome
}
```

### 5. Maintainable Tests

- Use test builders for complex objects
- Extract common setup to base classes
- Keep tests DRY but readable

## Coverage Goals

- **Unit Tests**: 80%+ coverage
- **Integration Tests**: Critical paths covered
- **Mutation Testing**: 70%+ mutation score

## Anti-Patterns to Avoid

### 1. Testing Implementation Details

```csharp
// Bad: Tests internal implementation
_service.Verify(x => x._privateField == expected);

// Good: Tests public behavior
result.Should().Be(expected);
```

### 2. Overuse of Mocks

```csharp
// Bad: Mocking value objects
var mock = new Mock<DateTime>();

// Good: Use real objects when possible
var testDate = new DateTime(2024, 1, 1);
```

### 3. Ignored Tests

```csharp
// Bad: Commented out or ignored tests
[Fact(Skip = "Fix later")]
public void BrokenTest() { }

// Good: Fix or remove broken tests
```

### 4. Test Logic

```csharp
// Bad: Complex logic in tests
if (condition) {
    Assert.True(result);
} else {
    Assert.False(result);
}

// Good: Simple, declarative assertions
result.Should().Be(expectedValue);
```

## Debugging Tests

### 1. Test Output

Use `ITestOutputHelper` for debugging:

```csharp
public TestClass(ITestOutputHelper output)
{
    _output = output;
}

[Fact]
public void TestMethod()
{
    _output.WriteLine($"Debug info: {variable}");
}
```

### 2. Test Logs

Enable detailed logging in tests:

```csharp
services.AddLogging(builder =>
{
    builder.AddXUnit(output);
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

### 3. Debugging Integration Tests

```csharp
// Capture HTTP traffic
var response = await Client.GetAsync("/api/puzzles");
_output.WriteLine($"Response: {await response.Content.ReadAsStringAsync()}");

// Inspect database state
var entities = await DbContext.Puzzles.ToListAsync();
_output.WriteLine($"Entity count: {entities.Count}");
```

## Summary

TDD is not just about testing—it's about:
- Designing better APIs
- Writing maintainable code
- Documenting behavior
- Preventing regressions
- Enabling confident refactoring

Remember: If it's not tested, it's broken!