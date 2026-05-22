<!-- SYNC IMPACT REPORT
Version change: [TEMPLATE] → 1.0.0
Modified principles: N/A (initial population from template)
Added sections:
  - Core Principles (I. Clean Architecture, II. Data Access, III. Authentication & Authorization,
    IV. API Documentation, V. Unit Testing)
  - Constraints & Standards
  - Governance
Removed sections: N/A
Templates requiring updates:
  - .specify/templates/plan-template.md ⚠️ pending — source tree includes tests/integration/ which
    conflicts with the unit-only testing mandate; update sample structure to tests/unit/ only
  - .specify/templates/tasks-template.md ⚠️ pending — sample tasks include "Contract test" and
    "Integration test" entries that violate the unit-only policy; replace with unit test examples
  - .specify/templates/spec-template.md ✅ no changes required
  - .specify/templates/checklist-template.md ✅ no changes required
Follow-up TODOs: None — all placeholders resolved
-->

# Lumen Constitution

## Core Principles

### I. Clean Architecture

The solution MUST maintain four layers: Domain, Application, Infrastructure, and API.
Dependencies MUST flow strictly inward — no layer may reference a layer above it.
Domain has no dependencies. Application depends on Domain only. Infrastructure depends on
Application and Domain. API depends on Infrastructure and Application.

Controllers are REQUIRED — Minimal API endpoints are NOT permitted. Every controller MUST be
thin: routing, model binding, and response shaping only. Business logic MUST reside exclusively
in the Application layer.

The Repository pattern MUST be used for all data access. Repository interfaces are DEFINED in
the Application layer and IMPLEMENTED in the Infrastructure layer.

### II. Data Access

Entity Framework Core is the ONLY permitted ORM. Raw SQL, Dapper, ADO.NET, or any other
data access library are PROHIBITED.

All database interactions MUST go through repository interfaces. Direct `DbContext` usage is
FORBIDDEN outside of the Infrastructure layer. The Application layer MUST NOT contain any
data access or EF Core references.

EF Core migrations MUST live in the Infrastructure project and be applied via the
`dotnet ef` CLI targeting the API startup project.

### III. Authentication & Authorization

JWT (JSON Web Tokens) is the ONLY authentication mechanism. External identity providers
(e.g. Auth0, Azure AD, Okta, IdentityServer) are NOT permitted.

All protected endpoints MUST carry an `[Authorize]` attribute. Anonymous access MUST be
explicitly declared with `[AllowAnonymous]` — implicit open access is PROHIBITED.

JWT configuration (issuer, audience, signing key) MUST be supplied via `appsettings` or
environment variables and MUST NOT be hardcoded.

### IV. API Documentation

OpenAPI/Swagger documentation MUST be kept current at all times. Every endpoint, request
model, and response model MUST be documented with XML doc comments and annotated with all
applicable `[ProducesResponseType]` attributes.

Swagger UI MUST be available in development and staging environments. It MUST NOT be exposed
in production.

Undocumented endpoints or missing response codes constitute a defect and MUST be resolved
before merge.

### V. Unit Testing

Each feature MUST have proper unit tests covering its Application-layer logic (services,
validators, mapping profiles) and domain rules.

Integration tests are PROHIBITED — the test suite MUST contain unit tests only. The test
project (Lumen.Tests) uses xUnit with Moq for fakes.

Tests MUST be deterministic, isolated, and free of external dependencies (no database, no
network, no file I/O). Any infrastructure concern MUST be mocked via the repository interfaces
defined in the Application layer.

### VI. Error Handling

All domain and application errors MUST extend `AppException` (defined in
`Lumen.Application.Common.Exceptions`). Specific subtypes (`NotFoundException`,
`ConflictException`, `ValidationException`) MUST be used — throwing raw `Exception` or
framework exceptions from application code is PROHIBITED.

All HTTP error responses MUST conform to RFC 7807 Problem Details format. Controllers MUST
use `Problem()` or `ValidationProblem()` for error results — custom JSON error objects are
PROHIBITED. The global `IExceptionHandler` middleware is the single unhandled-exception
boundary; individual controllers and services MUST NOT swallow exceptions silently.

Caught exceptions MUST always be logged before re-throwing or translating. Empty `catch`
blocks are PROHIBITED.

### VII. Async & Concurrency

All I/O operations MUST use `async`/`await`. Blocking calls (`.Result`, `.Wait()`,
`.GetAwaiter().GetResult()`) are PROHIBITED in ASP.NET Core code — they cause deadlocks
under the request synchronization context.

All async methods MUST carry the `Async` suffix (e.g. `GetUserAsync`, `SaveChangesAsync`).

`ConfigureAwait(false)` MUST be used in Infrastructure and Domain library code. It is not
required in controller or Application service code where the ambient context is intentional.

### VIII. Logging

`ILogger<T>` is the ONLY permitted logging abstraction. `Console.WriteLine`,
`Debug.WriteLine`, `Trace`, and any third-party logging facade (Serilog static API, NLog
static API) are PROHIBITED in production code — configure sinks via the host builder only.

Use structured log messages with named placeholders: `logger.LogError(ex, "Failed to load
user {UserId}", id)` — never string interpolation inside the message template.

Log levels MUST be used semantically: `LogError` for unhandled/unexpected failures,
`LogWarning` for handled application exceptions, `LogInformation` for significant business
events, `LogDebug` for developer diagnostics.

## Constraints & Standards

- All code MUST target **.NET 10** and use **C# latest language features** where they improve
  clarity (e.g. primary constructors, collection expressions, pattern matching).
- **Nullable reference types** are enabled solution-wide. Null warnings MUST be resolved — they
  MUST NOT be suppressed with `#pragma warning disable` or `!` null-forgiving operators unless
  the value is proven non-null by invariants.
- **Build quality**: `dotnet build` MUST produce zero warnings. The solution treats warnings as
  errors — a warning-filled build MUST NOT be merged.
- All dependencies MUST be registered and injected via **constructor injection**. The service
  locator pattern (`IServiceProvider.GetService`) is PROHIBITED outside of composition root
  (`Program.cs`).
- No business logic in controllers. No data access logic in the Application layer. Violations
  are treated as architecture defects.
- `appsettings.json` is for defaults. Secrets MUST be managed via user-secrets (dev),
  environment variables (CI/staging), or a secrets manager (production).
- **File-scoped namespaces** MUST be used: `namespace Lumen.Application.Features.Users;`
- **Explicit access modifiers** are required on all type members (`public`, `private`,
  `protected`, `internal`). Omitting the modifier is PROHIBITED.
- `var` MUST only be used when the type is unambiguous from the right-hand side.
- Records MUST be used for immutable DTOs and value objects. Mutable classes are permitted
  only for EF Core entities and service implementations.
- **Controller response type**: methods MUST return `ActionResult<T>`. Bare `T` or plain
  `IActionResult` returns are PROHIBITED. Request DTOs MUST use data annotation attributes
  (`[Required]`, `[MaxLength]`, `[Range]`) for validation — manual validation logic inside
  controllers is PROHIBITED.
- **Test naming**: test methods MUST follow the pattern
  `MethodName_Scenario_ExpectedBehavior` (e.g. `CreateUser_WithDuplicateEmail_ThrowsConflictException`).
  Each test class MUST correspond to a single subject under test.

## Governance

This constitution is the authoritative source of architectural and engineering rules for Lumen.
It supersedes any conflicting guidance in READMEs, PR comments, or verbal agreements.

**Amendment procedure**: Any change to this constitution MUST be documented as a pull request
that updates this file, increments the version (following semantic versioning), records the
amendment date, and includes a rationale comment. Changes that remove or redefine a principle
are MAJOR; additions or material expansions are MINOR; clarifications are PATCH.

**Compliance review**: Every PR MUST verify compliance with all active principles before merge.
The Constitution Check section of `plan.md` provides the gate checklist for each feature.

**Versioning policy**: `MAJOR.MINOR.PATCH` — bump MAJOR for backward-incompatible governance
changes, MINOR for new principles or sections, PATCH for wording clarifications.
