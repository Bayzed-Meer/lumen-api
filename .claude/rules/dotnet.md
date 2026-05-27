---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# .NET / C# Rules

## General C# Style
- Use explicit access modifiers on all members (`public`, `private`, `protected`, `internal`)
- Use `var` only when the type is obvious from the right-hand side (e.g. `new`, casts, literals) — use explicit types when the right-hand side is a method call whose return type isn't immediately clear
- MUST use `async`/`await` for all I/O — never `.Result`, `.Wait()`, or raw `Task` continuations (causes deadlocks in ASP.NET Core)
- Use `ConfigureAwait(false)` in library/infrastructure code; not needed in controller/application code
- Use property-based records for all DTOs (both request and response) — never positional records (property names at the call site prevent argument-order mistakes)
- Use `required` with `init` for all DTO properties: `public required string Foo { get; init; }`
- Each data annotation attribute must be on its own line above the property — never inline on the same line as the property
- Use `file`-scoped namespaces: `namespace Librify.Api.Features.Books;`
- Default to writing no comments; add one only when the _why_ is non-obvious (hidden constraint, workaround, subtle invariant)
- No premature abstractions — YAGNI: three similar lines are better than a wrong abstraction; do not build for hypothetical future requirements

## Member Ordering
- Within a class, order members: private fields and constants first, then public members, methods last

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
- For 201 responses, use `Created(string.Empty, response)` — do not use `CreatedAtAction`; we do not emit `Location` headers
- Do not use `[AllowAnonymous]` unless overriding a class-level or global `[Authorize]` policy — endpoints without `[Authorize]` are already anonymous by default

## Dependency Injection
- Register services in `Program.cs` or dedicated extension methods — never use `new`
- Prefer constructor injection; use `[FromServices]` in minimal API handlers
- Scope services appropriately: `Singleton` for stateless, `Scoped` for per-request, `Transient` for lightweight
- Use C# 12 primary constructors and reference the parameter directly — do not add a `private readonly` field assignment (e.g. `private readonly IFoo _foo = foo;`)

## EF Core
- Never expose `DbContext` outside the data layer — use repositories or direct service injection
- Use `AsNoTracking()` for read-only queries
- Never use raw SQL string interpolation — use `FromSqlInterpolated` or parameterized `ExecuteSqlRaw`
- Always handle `DbUpdateConcurrencyException` where optimistic concurrency applies

## Build Quality

- `dotnet build` must pass with zero warnings — warnings are treated as errors
- `dotnet test` must pass with zero failures before any commit

## ASP.NET Core Identity

- Use `AddIdentityCore<ApplicationUser>` not `AddIdentity` — APIs are JWT-only; `AddIdentity` adds cookie/UI overhead that is not needed
- Chain `.AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>()` to register `RoleManager` and EF stores
- `ApplicationUser` must live in `Lumen.Infrastructure` — it extends `IdentityUser` which is a framework type; Domain must stay dependency-free
- Always go through `UserManager` for user writes (create, password change, role assignment) — never write to `AspNetUsers` directly via `DbContext`; bypassing `UserManager` breaks password hashing and security stamps
- Use `DbContext` directly for complex user read queries (filtering, pagination, joins) — `UserManager` does not expose `IQueryable`
- Expose user operations to Application layer via `IIdentityService` (Application) / `IdentityService` (Infrastructure) — never reference `UserManager` from Application or API layers
- Do not create a `UserRepository` — `UserManager` is already the user repository abstraction

## Configuration & Options
- Never inject `IConfiguration` into services — use strongly-typed options classes instead
- Create a `sealed` options class with a `SectionName` constant, `required` properties, and `[Required]`/`[Range]` data annotations
- Register with `.AddOptions<T>().BindConfiguration(T.SectionName).ValidateDataAnnotations().ValidateOnStart()` — fails at startup on misconfiguration
- Inject `IOptions<T>` in services that only need the value at construction time
- `IConfiguration` is allowed only in `DependencyInjection.cs` (composition root) for wiring up infrastructure that cannot use `IOptions<T>` directly (e.g. `AddJwtBearer`, `AddDbContext`)

## Secrets & Configuration

- Never commit passwords or credentials — leave keys empty or absent in `appsettings.json`
- Dev: use `dotnet user-secrets set` — stored at `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`, never committed
- Prod: use environment variables with `__` as key separator (e.g. `AdminSeed__Password=...`)
- User secrets only load when `ASPNETCORE_ENVIRONMENT=Development` — ensure `launchSettings.json` sets this for `dotnet run`

## Error Handling
- Use `ILogger<T>` for all logging — never `Console.WriteLine` in production code
- Throw specific exception types — not `Exception` directly
- Let unhandled exceptions propagate to the global error handler (middleware)
- Never swallow exceptions in a catch block without logging
