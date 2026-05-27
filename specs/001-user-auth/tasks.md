---

description: "Task list for User Authentication & Role-Based Account Management"
---

# Tasks: User Authentication & Role-Based Account Management

**Input**: Design documents from `/specs/001-user-auth/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Unit tests are MANDATORY per the project constitution. Every feature MUST include unit tests for Application-layer logic. No integration or contract tests are permitted.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add new dependencies and configuration scaffolding before any implementation begins.

- [X] T001 Add `Microsoft.AspNetCore.Authentication.JwtBearer` to `src/Lumen.Infrastructure/Lumen.Infrastructure.csproj` and `src/Lumen.API/Lumen.API.csproj`; add `MailKit` to `src/Lumen.Infrastructure/Lumen.Infrastructure.csproj`
- [X] T00X Add `Jwt` (Issuer, Audience, AccessTokenExpiryMinutes, RefreshTokenExpiryDays — leave Key empty) and `Smtp` (Host, Port, FromAddress, FromName — leave Username/Password empty) config sections to `src/Lumen.API/appsettings.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, EF Core schema, JWT plumbing, and all interfaces that EVERY user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

> **Standing Implementation Rules** (apply to all tasks in this phase and beyond):
> - **File-scoped namespaces**: every new file MUST use `namespace Lumen.X.Y;` (not block-scoped braces)
> - **Explicit access modifiers**: every type member MUST declare `public`, `private`, `protected`, or `internal`
> - **`ConfigureAwait(false)`**: every `await` call in Infrastructure layer code MUST use `.ConfigureAwait(false)`
> - **Test naming**: test methods MUST follow `MethodName_Scenario_ExpectedBehavior` e.g. `CreateAccountAsync_WithDuplicateEmail_ThrowsConflictException`
> - **Records for DTOs**: all DTO types MUST be declared `public sealed record`
> - **Controller return types**: all controller actions MUST return `ActionResult<T>` — bare `T` or `IActionResult` are prohibited by the constitution

### Domain Layer

- [X] T00X [P] Create `UserRole` enum (`Admin, Librarian, Student, Faculty`) in `src/Lumen.Domain/Enums/UserRole.cs`
- [X] T00X [P] Create `Student` POCO entity (`Id: Guid`, `UserId: string`, `InstitutionalId: string max 50`) with no EF Core attributes or navigation properties in `src/Lumen.Domain/Entities/Student.cs`
- [X] T00X [P] Create `Faculty` POCO entity (`Id: Guid`, `UserId: string`, `InstitutionalId: string max 50`) in `src/Lumen.Domain/Entities/Faculty.cs`
- [X] T00X [P] Create `Librarian` POCO entity (`Id: Guid`, `UserId: string`, `InstitutionalId: string max 50`) in `src/Lumen.Domain/Entities/Librarian.cs`
- [X] T00X [P] Create `OtpRecord` POCO entity (`Id: Guid`, `UserId: string`, `CodeHash: string`, `IssuedAt: DateTimeOffset`, `ExpiresAt: DateTimeOffset`, `IsInvalidated: bool default false`, `FailedAttempts: int default 0`) in `src/Lumen.Domain/Entities/OtpRecord.cs`

### Application Layer — Common Types

- [X] T00X [P] Create `Roles` static class with string constants `Admin`, `Librarian`, `Student`, `Faculty` (values via `nameof(UserRole.*)`) in `src/Lumen.Application/Common/Constants/Roles.cs`
- [X] T00X [P] Create `ForbiddenException : AppException` (HTTP 403) in `src/Lumen.Application/Common/Exceptions/ForbiddenException.cs`
- [X] T0XX [P] Create `UnauthorizedException : AppException` (HTTP 401) in `src/Lumen.Application/Common/Exceptions/UnauthorizedException.cs`
- [X] T0XX Extend `IIdentityService` with: `CreateUserAsync(string email, string firstName, string lastName, string password, UserRole role, string institutionalId): Task<string>` — returns the new `userId`; the Infrastructure `IdentityService` implementation MUST atomically create both the `ApplicationUser` via `UserManager` AND the matching profile entity (`Student`/`Faculty`/`Librarian`) via `AppDbContext`, so the Application layer NEVER touches `AppDbContext` directly; `FindUserByEmailAsync(string email): Task<string?>` (returns userId or null); `FindUserByInstitutionalIdAsync(string id): Task<string?>` (returns userId or null); `CheckPasswordAsync(string userId, string password): Task<bool>`; `GetUserRoleAsync(string userId): Task<UserRole>`; `IsVerifiedAsync(string userId): Task<bool>`; `SetVerifiedAsync(string userId): Task` in `src/Lumen.Application/Common/Interfaces/IIdentityService.cs`
- [X] T0XX [P] Create `IEmailService` interface with `SendOtpEmailAsync(string toEmail, string otp)` in `src/Lumen.Application/Common/Interfaces/IEmailService.cs`
- [X] T0XX [P] Create `ITokenService` interface with `GenerateAccessToken(string userId, string email, string role): string` and `GenerateRefreshToken(): string` — do NOT reference `ApplicationUser` here; `ApplicationUser` is an Infrastructure type and any reference to it from Application would violate Clean Architecture in `src/Lumen.Application/Common/Interfaces/ITokenService.cs`
- [X] T0XX [P] Create `IOtpRepository` interface with `AddAsync`, `GetActiveOtpAsync(string userId)`, `InvalidateAllForUserAsync(string userId)`, `UpdateAsync` in `src/Lumen.Application/Common/Interfaces/IOtpRepository.cs`
- [X] T0XX [P] Create `IRefreshTokenRepository` interface with `AddAsync`, `GetByTokenHashAsync(string hash)`, `RevokeTokenAsync`, `RevokeTokenFamilyAsync(string userId)` in `src/Lumen.Application/Common/Interfaces/IRefreshTokenRepository.cs`

### Infrastructure Layer — Identity

- [X] T0XX [P] Extend `ApplicationUser` with properties `FirstName` (string, required, max 100), `LastName` (string, required, max 100), `Role` (UserRole, required), `IsVerified` (bool, default false), `CreatedAt` (DateTimeOffset, required) in `src/Lumen.Infrastructure/Identity/ApplicationUser.cs`
- [X] T0XX [P] Extend `AdminSeedOptions` with `FirstName` (string) and `LastName` (string) properties in `src/Lumen.Infrastructure/Identity/AdminSeedOptions.cs`
- [X] T0XX Update `AdminSeeder` to set `FirstName`, `LastName` (from updated `AdminSeedOptions`), `Role = UserRole.Admin`, `IsVerified = true`, `CreatedAt = DateTimeOffset.UtcNow` when creating the admin `ApplicationUser` in `src/Lumen.Infrastructure/Identity/AdminSeeder.cs`

### Infrastructure Layer — Entities & EF Core

- [X] T0XX [P] Create `RefreshToken` Infrastructure entity (`Id: Guid`, `UserId: string`, `TokenHash: string`, `ExpiresAt: DateTimeOffset`, `IsRevoked: bool default false`, `ReplacedByTokenId: Guid?`, `CreatedAt: DateTimeOffset`) in `src/Lumen.Infrastructure/Entities/RefreshToken.cs`
- [X] T0XX [P] Create `StudentConfiguration : IEntityTypeConfiguration<Student>` (FK to `AspNetUsers.Id`, unique index on `InstitutionalId`) in `src/Lumen.Infrastructure/Data/Configurations/StudentConfiguration.cs`
- [X] T0XX [P] Create `FacultyConfiguration : IEntityTypeConfiguration<Faculty>` (FK to `AspNetUsers.Id`, unique index on `InstitutionalId`) in `src/Lumen.Infrastructure/Data/Configurations/FacultyConfiguration.cs`
- [X] T0XX [P] Create `LibrarianConfiguration : IEntityTypeConfiguration<Librarian>` (FK to `AspNetUsers.Id`, unique index on `InstitutionalId`) in `src/Lumen.Infrastructure/Data/Configurations/LibrarianConfiguration.cs`
- [X] T0XX [P] Create `OtpRecordConfiguration : IEntityTypeConfiguration<OtpRecord>` (FK to `AspNetUsers.Id`, index on `UserId`) in `src/Lumen.Infrastructure/Data/Configurations/OtpRecordConfiguration.cs`
- [X] T0XX [P] Create `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>` (FK to `AspNetUsers.Id`, unique index on `TokenHash`, self-ref FK `ReplacedByTokenId → RefreshToken.Id` nullable) in `src/Lumen.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs`
- [X] T0XX Add `DbSet<Student> Students`, `DbSet<Faculty> Faculty`, `DbSet<Librarian> Librarians`, `DbSet<OtpRecord> OtpRecords`, `DbSet<RefreshToken> RefreshTokens` properties to `AppDbContext` in `src/Lumen.Infrastructure/Data/AppDbContext.cs`
- [X] T0XX Implement all new `IIdentityService` methods in `IdentityService`: `CreateUserAsync` — call `UserManager.CreateAsync` for the `ApplicationUser` then immediately create the matching profile entity (`Student`/`Faculty`/`Librarian`) via `AppDbContext.Add` + `SaveChangesAsync`; wrap both in a transaction so either both succeed or both roll back; `FindUserByEmailAsync` (query `UserManager.FindByEmailAsync`); `FindUserByInstitutionalIdAsync` (sequential LINQ query across Students/Faculty/Librarians — first match wins, ≤3 queries); `CheckPasswordAsync` (`UserManager.CheckPasswordAsync`); `GetUserRoleAsync` (read `ApplicationUser.Role`); `IsVerifiedAsync` (read `ApplicationUser.IsVerified`); `SetVerifiedAsync` (update `IsVerified = true` + `UserManager.UpdateAsync`) in `src/Lumen.Infrastructure/Identity/IdentityService.cs`
- [X] T0XX [P] Create `OtpRepository : IOtpRepository` backed by `AppDbContext` in `src/Lumen.Infrastructure/Repositories/OtpRepository.cs`
- [X] T0XX [P] Create `RefreshTokenRepository : IRefreshTokenRepository` backed by `AppDbContext` (family revocation walks `ReplacedByTokenId` chain) in `src/Lumen.Infrastructure/Repositories/RefreshTokenRepository.cs`
- [X] T0XX [P] Create `SmtpEmailService : IEmailService` using MailKit `SmtpClient`; read host/port/credentials from `IOptions<SmtpOptions>`; log `LogWarning` on send failure without rethrowing in `src/Lumen.Infrastructure/Services/SmtpEmailService.cs`
- [X] T0XX [P] Create `TokenService : ITokenService`; `GenerateAccessToken` builds a JWT with claims (`sub`, `email`, `role`, `jti`, `exp`) signed with HMAC-SHA256 key from `IOptions<JwtOptions>`; `GenerateRefreshToken` returns a cryptographically random opaque string in `src/Lumen.Infrastructure/Services/TokenService.cs`
- [X] T0XX Register `IEmailService → SmtpEmailService`, `ITokenService → TokenService`, `IOtpRepository → OtpRepository`, `IRefreshTokenRepository → RefreshTokenRepository`; configure `JwtBearer` authentication using `JwtOptions`; configure `SmtpOptions` from config; configure `CookieSecurePolicy.SameAsRequest` in dev; configure `IdentityOptions.Password` with `RequiredLength = 8`, `RequireUppercase = true`, `RequireLowercase = true`, `RequireDigit = true`, `RequireNonAlphanumeric = true` (FR-005a) in `src/Lumen.Infrastructure/DependencyInjection.cs`
- [X] T0XX Add `app.UseAuthentication()` and `app.UseAuthorization()` after existing middleware; enable OpenAPI XML doc generation (`<GenerateDocumentationFile>true</GenerateDocumentationFile>` in project file, `IncludeXmlComments` in Swagger config) in `src/Lumen.API/Program.cs`
- [X] T0XX Run `dotnet ef migrations add AddAuthEntities --project src/Lumen.Infrastructure --startup-project src/Lumen.API` to generate the migration that adds new columns to `AspNetUsers` and creates `Students`, `Faculty`, `Librarians`, `OtpRecords`, `RefreshTokens` tables with all indexes
- [X] T0XX Run `dotnet build Lumen.slnx` and confirm zero warnings and zero errors before proceeding to any user story

**Checkpoint**: Foundation ready — all user story phases can now begin.

---

## Phase 3: User Story 1 — Admin Creates Any Account (Priority: P1) 🎯 MVP

**Goal**: An authenticated admin can create a librarian, student, or faculty account. The account is created unverified and an OTP is dispatched to the new user's email.

**Independent Test**: Log in as admin (seeded at startup), call `POST /api/register` with valid librarian details, confirm 201 response with `isVerified: false`, confirm an `OtpRecord` row was created and `SmtpEmailService` was called.

### Tests for User Story 1 (mandatory per constitution)

- [X] T0XX [P] [US1] Write unit tests for `RegisterService.CreateAccountAsync`: admin creates librarian → success (201, IsVerified false, OTP dispatched); duplicate email → `ConflictException`; duplicate institutionalId → `ConflictException`; missing required field → `ValidationException`; OTP email failure → account still created (201) in `tests/Lumen.Tests/Features/Auth/RegisterServiceTests.cs`

### Implementation for User Story 1

- [X] T0XX [P] [US1] Create `public sealed record CreateAccountRequest` with properties `InstitutionalId` (`[Required][MaxLength(50)]`), `Email` (`[Required][EmailAddress]`), `FirstName` (`[Required][MaxLength(100)]`), `LastName` (`[Required][MaxLength(100)]`), `Password` (`[Required]`), `Role` (`[Required]`); `Role` MUST be constrained to `Student`, `Faculty`, or `Librarian` — Admin is not creatable via this endpoint (add `[AllowedValues("Student", "Faculty", "Librarian")]` or an equivalent validation) in `src/Lumen.Application/Features/Auth/DTOs/CreateAccountRequest.cs`
- [X] T0XX [P] [US1] Create `public sealed record CreateAccountResponse(string UserId, string Email, string Role, bool IsVerified)` in `src/Lumen.Application/Features/Auth/DTOs/CreateAccountResponse.cs`
- [X] T0XX [P] [US1] Create `IRegisterService` interface with `CreateAccountAsync(CreateAccountRequest request, string callerRole): Task<CreateAccountResponse>` signature in `src/Lumen.Application/Features/Auth/Interfaces/IRegisterService.cs`
- [X] T0XX [US1] Implement `RegisterService : IRegisterService` with `CreateAccountAsync`: call `IIdentityService.CreateUserAsync(email, firstName, lastName, password, role, institutionalId)` — Infrastructure handles both `ApplicationUser` + profile entity creation atomically; this Application service MUST NOT reference `AppDbContext`; generate a 6-digit OTP via `RandomNumberGenerator.GetInt32(100000, 1000000)`, hash with SHA-256 (`Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)))`), persist via `IOtpRepository.AddAsync`; call `IEmailService.SendOtpEmailAsync` — on exception call `_logger.LogWarning(ex, "OTP email dispatch failed for {UserId}", userId)` then return normally (do NOT rethrow per FR-010); return `CreateAccountResponse` in `src/Lumen.Application/Features/Auth/Services/RegisterService.cs`
- [X] T0XX [US1] Create `RegisterController` with `POST /api/register` action returning `ActionResult<CreateAccountResponse>`: `[Authorize(Roles = "Admin,Librarian")]`, read caller role from JWT `ClaimTypes.Role`, call `IRegisterService.CreateAccountAsync`, return `CreatedAtAction(...)` (201) with `CreateAccountResponse`; add `[ProducesResponseType<CreateAccountResponse>(201)]`, `[ProducesResponseType(400)]`, `[ProducesResponseType(401)]`, `[ProducesResponseType(403)]`, `[ProducesResponseType(409)]` and XML `<summary>` doc comment in `src/Lumen.API/Controllers/RegisterController.cs`

**Checkpoint**: Admin can create any role account end-to-end. User Story 1 independently verifiable.

---

## Phase 4: User Story 2 & 7 — Librarian Restrictions & Student/Faculty Blocked (Priority: P2)

**Goal**: Librarians can create student/faculty accounts but not librarian accounts (US2). Students and faculty receive 403 for any account-creation attempt (US7 — enforced by `[Authorize(Roles = "Admin,Librarian")]` already applied in T040).

**Independent Test (US2)**: Log in as a librarian, create a student account → 201. Attempt to create a librarian account → 403.
**Independent Test (US7)**: Log in as a student, call `POST /api/register` → 403. Repeat as faculty → 403.

### Tests for User Story 2 & 7 (mandatory per constitution)

- [X] T0XX [P] [US2] Write unit tests for `RegisterService.CreateAccountAsync` role enforcement: caller role `Librarian` + target role `Student` → success; caller role `Librarian` + target role `Faculty` → success; caller role `Librarian` + target role `Librarian` → `ForbiddenException` in `tests/Lumen.Tests/Features/Auth/RegisterServiceTests.cs`

### Implementation for User Story 2 & 7

- [X] T0XX [US2] Extend `RegisterService.CreateAccountAsync` to throw `ForbiddenException("Librarians may not create accounts for other librarians.")` when `callerRole == Roles.Librarian && request.Role == Roles.Librarian` (fine-grained gate, runs after the coarse `[Authorize]` gate on the controller) in `src/Lumen.Application/Features/Auth/Services/RegisterService.cs`

> **Note (US7)**: Student and faculty callers are already blocked by `[Authorize(Roles = "Admin,Librarian")]` on `RegisterController` (T040); no additional service-layer code is needed.

**Checkpoint**: All role-permission constraints enforced. US2 and US7 independently verifiable.

---

## Phase 5: User Story 3 — New User Verifies Account via OTP (Priority: P3)

**Goal**: A new user submits their OTP to the verify endpoint; on success their account becomes verified and they may log in.

**Independent Test**: Create a student account (US1), retrieve the `OtpRecord.CodeHash` from the DB (or capture the plaintext OTP via a mocked email service), call `POST /api/register/verify` with the correct OTP, confirm 200 and `ApplicationUser.IsVerified = true`.

### Tests for User Story 3 (mandatory per constitution)

- [X] T0XX [P] [US3] Write unit tests for `RegisterService.VerifyOtpAsync`: correct OTP → account verified (200); wrong OTP → `ValidationException` + `FailedAttempts` incremented; 5 consecutive wrong OTPs → `ValidationException` locked; expired OTP → `ValidationException`; already-verified account → `ConflictException`; account not found → `NotFoundException` in `tests/Lumen.Tests/Features/Auth/RegisterServiceTests.cs`

### Implementation for User Story 3

- [X] T0XX [P] [US3] Create `public sealed record VerifyOtpRequest([Required] string Identity, [Required][RegularExpression(@"^\d{6}$")] string Otp)` in `src/Lumen.Application/Features/Auth/DTOs/VerifyOtpRequest.cs`
- [X] T0XX [P] [US3] Extend `IRegisterService` with `VerifyOtpAsync(VerifyOtpRequest request): Task` in `src/Lumen.Application/Features/Auth/Interfaces/IRegisterService.cs`
- [X] T0XX [US3] Implement `RegisterService.VerifyOtpAsync`: resolve identity (email → `FindUserByEmailAsync`; else → `FindUserByInstitutionalIdAsync`); check `IsVerified` → throw `ConflictException` if already verified; find active OTP (`!IsInvalidated && ExpiresAt > UtcNow`); check `FailedAttempts >= 5` → throw locked error; compare SHA-256 hash of submitted OTP → on mismatch increment `FailedAttempts` + `UpdateAsync` → throw `ValidationException`; on match set `IsInvalidated = true` + call `IIdentityService.SetVerifiedAsync` in `src/Lumen.Application/Features/Auth/Services/RegisterService.cs`
- [X] T0XX [US3] Add `POST /api/register/verify` action returning `ActionResult` to `RegisterController` with `[AllowAnonymous]`; call `IRegisterService.VerifyOtpAsync`; return `Ok(new { message = "Account verified successfully." })`; add `[ProducesResponseType(200)]`, `[ProducesResponseType(400)]`, `[ProducesResponseType(404)]`, `[ProducesResponseType(409)]` and XML `<summary>` in `src/Lumen.API/Controllers/RegisterController.cs`

**Checkpoint**: New users can verify their accounts. User Story 3 independently verifiable.

---

## Phase 6: User Story 4 — Resend OTP (Priority: P4)

**Goal**: An unverified user requests a new OTP; the previous OTP is invalidated and a fresh code is dispatched to their email.

**Independent Test**: Create an account (US1), call `POST /api/register/resend-otp`, confirm `OtpRecord` for the previous code is now `IsInvalidated = true` and a new active `OtpRecord` exists.

### Tests for User Story 4 (mandatory per constitution)

- [X] T0XX [P] [US4] Write unit tests for `RegisterService.ResendOtpAsync`: unverified account → old OTP invalidated, new OTP created, email dispatched (200); already-verified account → `ValidationException` (400); account not found → `NotFoundException` (404) in `tests/Lumen.Tests/Features/Auth/RegisterServiceTests.cs`

### Implementation for User Story 4

- [X] T0XX [P] [US4] Create `public sealed record ResendOtpRequest([Required] string Identity)` in `src/Lumen.Application/Features/Auth/DTOs/ResendOtpRequest.cs`
- [X] T0XX [P] [US4] Extend `IRegisterService` with `ResendOtpAsync(ResendOtpRequest request): Task` in `src/Lumen.Application/Features/Auth/Interfaces/IRegisterService.cs`
- [X] T0XX [US4] Implement `RegisterService.ResendOtpAsync`: resolve identity; throw `ValidationException` if already verified; call `IOtpRepository.InvalidateAllForUserAsync` to mark all existing OTPs `IsInvalidated = true`; generate new 6-digit OTP, hash, persist; call `IEmailService.SendOtpEmailAsync` in `src/Lumen.Application/Features/Auth/Services/RegisterService.cs`
- [X] T0XX [US4] Add `POST /api/register/resend-otp` action returning `ActionResult` to `RegisterController` with `[AllowAnonymous]`; call `IRegisterService.ResendOtpAsync`; return `Ok(new { message = "A new OTP has been sent to the registered email address." })`; add `[ProducesResponseType(200)]`, `[ProducesResponseType(400)]`, `[ProducesResponseType(404)]` and XML `<summary>` in `src/Lumen.API/Controllers/RegisterController.cs`

**Checkpoint**: Users can recover from failed OTP delivery. User Story 4 independently verifiable.

---

## Phase 7: User Story 5 — User Login (Priority: P5)

**Goal**: A verified user authenticates with email+password or institutionalId+password and receives a JWT access token in the body plus a refresh token in an HttpOnly cookie.

**Independent Test**: Verify a test account (US3), call `POST /api/auth/login` with valid email+password → 200 with `accessToken`. Call again with the institutional ID → 200 with `accessToken`. Call with unverified account or wrong credentials → 401 generic message.

### Tests for User Story 5 (mandatory per constitution)

- [X] T0XX [P] [US5] Write unit tests for `AuthService.LoginAsync`: valid email+password → `LoginResponse` returned + refresh token created; valid institutionalId+password → success; unverified account → `UnauthorizedException`; wrong password → `UnauthorizedException`; non-existent identity → `UnauthorizedException` (generic error — no identity disclosure) in `tests/Lumen.Tests/Features/Auth/AuthServiceTests.cs`

### Implementation for User Story 5

- [X] T0XX [P] [US5] Create `public sealed record LoginRequest([Required] string Identity, [Required] string Password)` in `src/Lumen.Application/Features/Auth/DTOs/LoginRequest.cs`
- [X] T0XX [P] [US5] Create `public sealed record LoginResponse(string AccessToken, int AccessTokenExpiresIn)` — no `RefreshToken` property; refresh token is cookie-only in `src/Lumen.Application/Features/Auth/DTOs/LoginResponse.cs`
- [X] T0XX [P] [US5] Create `IAuthService` interface with `LoginAsync(LoginRequest): Task<(LoginResponse response, string rawRefreshToken)>`, `LogoutAsync(string refreshTokenHash): Task`, `RefreshAsync(string rawRefreshToken): Task<(LoginResponse response, string newRawRefreshToken)>` in `src/Lumen.Application/Features/Auth/Interfaces/IAuthService.cs`
- [X] T0XX [US5] Implement `AuthService.LoginAsync`: call `IIdentityService.FindUserByEmailAsync` then `FindUserByInstitutionalIdAsync` (first non-null wins); throw `UnauthorizedException("Invalid credentials.")` for any failure — no identity disclosure (FR-019); call `IIdentityService.CheckPasswordAsync`; call `IIdentityService.IsVerifiedAsync`; call `IIdentityService.GetUserRoleAsync` to get role as string; call `ITokenService.GenerateAccessToken(userId, email, roleName)` — pass plain strings, NEVER pass `ApplicationUser`; call `ITokenService.GenerateRefreshToken()`; hash raw token with SHA-256; persist `RefreshToken` record via `IRefreshTokenRepository.AddAsync`; return `(LoginResponse, rawRefreshToken)` tuple in `src/Lumen.Application/Features/Auth/Services/AuthService.cs`
- [X] T0XX [US5] Create `AuthController` with `POST /api/auth/login` action returning `ActionResult<LoginResponse>` and `[AllowAnonymous]`; call `IAuthService.LoginAsync`; set cookie via `Response.Cookies.Append("refreshToken", rawToken, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/auth", MaxAge = TimeSpan.FromDays(7) })`; return `Ok(loginResponse)`; add `[ProducesResponseType<LoginResponse>(200)]`, `[ProducesResponseType(401)]` and XML `<summary>` in `src/Lumen.API/Controllers/AuthController.cs`
- [X] T0XX [US5] Add `POST /api/auth/refresh` action returning `ActionResult<LoginResponse>` with `[AllowAnonymous]` to `AuthController`; read cookie via `Request.Cookies["refreshToken"]`; call `IAuthService.RefreshAsync`; replace cookie (same `CookieOptions` as T058); return `Ok(loginResponse)`; add `[ProducesResponseType<LoginResponse>(200)]`, `[ProducesResponseType(401)]` and XML `<summary>` in `src/Lumen.API/Controllers/AuthController.cs`

**Checkpoint**: Verified users can authenticate and receive tokens. User Story 5 independently verifiable.

---

## Phase 8: User Story 6 — Logout (Priority: P6)

**Goal**: An authenticated user revokes their refresh token; the refresh cookie is cleared. Other sessions remain active. The access token expires naturally within its remaining lifetime (≤ 15 min).

**Independent Test**: Log in (get access token + refresh cookie), call `POST /api/auth/logout` → 204, then call `POST /api/auth/refresh` with the same cookie → 401.

### Tests for User Story 6 (mandatory per constitution)

- [X] T0XX [P] [US6] Write unit tests for `AuthService.LogoutAsync`: valid token hash → `IsRevoked = true` on the matching `RefreshToken` record; unknown token hash → no error (idempotent). Write unit tests for `AuthService.RefreshAsync`: valid token → old revoked + new pair returned; revoked token presented (reuse detection) → `UnauthorizedException` + entire family revoked in `tests/Lumen.Tests/Features/Auth/AuthServiceTests.cs`

### Implementation for User Story 6

- [X] T0XX [US6] Implement `AuthService.LogoutAsync`: hash the incoming raw token with SHA-256; call `IRefreshTokenRepository.RevokeTokenAsync(hash)` in `src/Lumen.Application/Features/Auth/Services/AuthService.cs`
- [X] T0XX [US6] Implement `AuthService.RefreshAsync`: hash the incoming raw token; call `GetByTokenHashAsync`; if token is `IsRevoked` → call `RevokeTokenFamilyAsync(userId)` → throw `UnauthorizedException`; if expired → throw `UnauthorizedException`; revoke old token; generate new access + refresh token pair; persist new `RefreshToken` with `ReplacedByTokenId = oldToken.Id`; return new pair in `src/Lumen.Application/Features/Auth/Services/AuthService.cs`
- [X] T0XX [US6] Add `POST /api/auth/logout` action returning `ActionResult` with `[Authorize]` to `AuthController`; read `refreshToken` cookie; call `IAuthService.LogoutAsync`; clear cookie via `Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" })`; return `NoContent()` (204); add `[ProducesResponseType(204)]`, `[ProducesResponseType(401)]` and XML `<summary>` in `src/Lumen.API/Controllers/AuthController.cs`

**Checkpoint**: Users can securely log out. Token reuse detection is active. User Story 6 independently verifiable.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, error mapping, and final validation across all stories.

- [X] T0XX [P] Audit all `RegisterController` actions: verify every action has complete `[ProducesResponseType]` coverage and XML `<summary>` / `<param>` / `<response>` doc comments matching the contracts in `contracts/register.md`; fix any gaps found in `src/Lumen.API/Controllers/RegisterController.cs`
- [X] T0XX [P] Audit all `AuthController` actions: verify complete `[ProducesResponseType]` coverage and XML doc comments matching `contracts/auth.md`; fix any gaps in `src/Lumen.API/Controllers/AuthController.cs`
- [X] T0XX Extend the existing `GlobalExceptionHandler` (or equivalent middleware) to map `ForbiddenException → 403 Problem Details` and `UnauthorizedException → 401 Problem Details` — check current handler for existing pattern to follow
- [X] T0XX Run `dotnet test` and confirm all `RegisterServiceTests` and `AuthServiceTests` pass with no failures
- [X] T0XX Run `dotnet build Lumen.slnx` and confirm zero warnings — resolve any CS warnings introduced during implementation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS ALL user stories**
- **User Story Phases (3–8)**: All depend on Phase 2 completion; can proceed sequentially by priority (P1 → P2 → P3 → P4 → P5 → P6)
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: First story — no dependencies on other stories
- **US2+US7 (P2)**: Depends on US1 controller and service scaffolding existing (Phase 3)
- **US3 (P3)**: Depends on US1 (accounts must exist to verify)
- **US4 (P4)**: Depends on US3 (VerifyOtpAsync is in the same service as ResendOtpAsync)
- **US5 (P5)**: Depends on US3 (verified accounts must exist to log in)
- **US6 (P6)**: Depends on US5 (must be able to log in to log out)

### Within Each User Story

1. Tests (write first — confirm they FAIL before implementation)
2. DTOs (data shapes)
3. Interface extension
4. Service implementation (depends on DTOs + interface)
5. Controller endpoint (depends on service)

### Parallel Opportunities per Story

- All `[P]`-marked tasks within a story can run simultaneously
- All `[P]`-marked Domain + Application interface tasks in Phase 2 can run simultaneously (different files)
- All `[P]`-marked EF configuration tasks (T020–T024) can run simultaneously
- All `[P]`-marked Infrastructure service/repo tasks (T027–T030) can run simultaneously

---

## Parallel Execution Examples

### Phase 2 — Foundational (launch together)

```
Batch A (domain entities — all independent files):
  T003: UserRole enum
  T004: Student entity
  T005: Faculty entity
  T006: Librarian entity
  T007: OtpRecord entity

Batch B (application interfaces — all independent files):
  T008: Roles.cs
  T009: ForbiddenException
  T010: UnauthorizedException
  T012: IEmailService
  T013: ITokenService
  T014: IOtpRepository
  T015: IRefreshTokenRepository

Batch C (EF Core configs — all independent files):
  T020: StudentConfiguration
  T021: FacultyConfiguration
  T022: LibrarianConfiguration
  T023: OtpRecordConfiguration
  T024: RefreshTokenConfiguration

Batch D (Infrastructure impls — all independent files):
  T027: OtpRepository
  T028: RefreshTokenRepository
  T029: SmtpEmailService
  T030: TokenService
```

### Phase 3 — US1 (launch together before implementation)

```
Parallel before T039:
  T035: CreateAccountRequest DTO
  T036: CreateAccountResponse DTO
  T037: IRegisterService interface
  T038: Unit tests (write + confirm FAIL)
```

### Phase 7 — US5 (launch together before implementation)

```
Parallel before T057:
  T053: LoginRequest DTO
  T054: LoginResponse DTO
  T055: IAuthService interface
  T056: Unit tests (write + confirm FAIL)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL — blocks all stories**)
3. Complete Phase 3: User Story 1 (admin creates accounts + OTP dispatch)
4. **STOP and VALIDATE**: Admin can create accounts; accounts are unverified; OTP is sent
5. Deploy / demo if ready

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. Phase 3 (US1) → Admin can create accounts → **MVP**
3. Phase 4 (US2+US7) → Full role enforcement active
4. Phase 5 (US3) → Users can verify accounts
5. Phase 6 (US4) → OTP recovery available
6. Phase 7 (US5) → Users can log in
7. Phase 8 (US6) → Users can log out securely
8. Phase 9 → Production-ready polish

---

## Task Summary

| Scope | Count |
|-------|-------|
| Phase 1: Setup | 2 |
| Phase 2: Foundational | 32 |
| Phase 3: US1 — Admin Creates Any Account | 6 |
| Phase 4: US2+US7 — Librarian Restrictions & Student/Faculty Blocked | 2 |
| Phase 5: US3 — OTP Verification | 5 |
| Phase 6: US4 — Resend OTP | 5 |
| Phase 7: US5 — User Login | 7 |
| Phase 8: US6 — Logout | 4 |
| Phase 9: Polish | 5 |
| **Total** | **68** |

**Parallelizable tasks**: 39 (marked `[P]`)
**Suggested MVP scope**: Phases 1–3 (T001–T040, 40 tasks)
