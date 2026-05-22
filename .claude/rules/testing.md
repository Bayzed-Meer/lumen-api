---
paths:
  - "**/*.Tests/**/*.cs"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# Testing Rules

## Framework & Structure
- Use **xUnit** for all tests — not MSTest or NUnit
- Use **FluentAssertions** for assertions — not raw `Assert.*`
- Use **Moq** for mocking — never hand-roll fakes in production test code
- One test class per production class; file name mirrors the class under test with `Tests` suffix
- Arrange / Act / Assert sections separated by a blank line

## Test Naming
- Method name format: `MethodName_StateUnderTest_ExpectedBehavior`
- Example: `GetBook_WhenBookDoesNotExist_Returns404`
- Use `[Fact]` for single-case tests, `[Theory]` + `[InlineData]` / `[MemberData]` for parameterized

## What to Test
- **Required**: every public Application-layer service method — happy path, every error/exception path, every branch in business logic
- **Not required**: controller actions, repository methods, trivial property getters/setters, auto-generated code, framework internals

## Scope
Only service-level unit tests are written for this project. Controller and repository tests are explicitly out of scope.

- Mock all dependencies (repositories, JWT service, logger, etc.) — never touch a real database or HTTP pipeline in tests
- Place tests in `tests/Librify.Tests/Services/`

## Pattern
```csharp
// Arrange
var sut = new BookService(_mockRepo.Object, _mockLogger.Object);

// Act
var result = await sut.GetByIdAsync(bookId);

// Assert
result.Should().NotBeNull();
result!.Title.Should().Be("Expected Title");
```

## Mandatory Workflow
After writing implementation code, agents MUST:
1. Write xUnit unit tests for every new Application-layer service method
2. Run `dotnet test` and fix all failures before committing
3. Do NOT commit with failing or skipped tests
