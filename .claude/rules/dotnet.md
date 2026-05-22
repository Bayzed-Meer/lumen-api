---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# .NET / C# Rules

## General C# Style
- Use explicit access modifiers on all members (`public`, `private`, `protected`, `internal`)
- Use `var` only when the type is obvious from the right-hand side
- MUST use `async`/`await` for all I/O — never `.Result`, `.Wait()`, or raw `Task` continuations (causes deadlocks in ASP.NET Core)
- Use `ConfigureAwait(false)` in library/infrastructure code; not needed in controller/application code
- Prefer records for immutable DTOs and value objects
- Use `required` properties instead of nullable types for mandatory fields
- Use `file`-scoped namespaces: `namespace Librify.Api.Features.Books;`
- Default to writing no comments; add one only when the _why_ is non-obvious (hidden constraint, workaround, subtle invariant)
- No premature abstractions — YAGNI: three similar lines are better than a wrong abstraction; do not build for hypothetical future requirements

## Naming Conventions
- Types, methods, properties: `PascalCase`
- Local variables, parameters: `camelCase`
- Private fields: `_camelCase`
- Constants: `PascalCase` (not `ALL_CAPS`)
- Interfaces: prefix with `I` (e.g. `IBookRepository`)

## ASP.NET Core Controllers
- Controllers must be thin — delegate all business logic to services
- Use `[ApiController]` attribute on every controller
- Return `ActionResult<T>` not bare `T` or `IActionResult` alone
- Use `[ProducesResponseType]` for all expected status codes
- Validate with `[Required]`, `[MaxLength]`, `[Range]` — never validate manually in controllers
- Return `Problem()` / `ValidationProblem()` for errors, not custom error objects

## Dependency Injection
- Register services in `Program.cs` or dedicated extension methods — never use `new`
- Prefer constructor injection; use `[FromServices]` in minimal API handlers
- Scope services appropriately: `Singleton` for stateless, `Scoped` for per-request, `Transient` for lightweight

## EF Core
- Never expose `DbContext` outside the data layer — use repositories or direct service injection
- Use `AsNoTracking()` for read-only queries
- Never use raw SQL string interpolation — use `FromSqlInterpolated` or parameterized `ExecuteSqlRaw`
- Always handle `DbUpdateConcurrencyException` where optimistic concurrency applies

## Build Quality

- `dotnet build` must pass with zero warnings — warnings are treated as errors
- `dotnet test` must pass with zero failures before any commit

## Error Handling
- Use `ILogger<T>` for all logging — never `Console.WriteLine` in production code
- Throw specific exception types — not `Exception` directly
- Let unhandled exceptions propagate to the global error handler (middleware)
- Never swallow exceptions in a catch block without logging
