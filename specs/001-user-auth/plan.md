# Implementation Plan: User Authentication & Role-Based Account Management

**Branch**: `001-user-auth` | **Date**: 2026-05-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-user-auth/spec.md`

## Summary

Implement full user authentication and role-based account management for the Lumen library system. Admins can create any account; librarians can create student/faculty only; students and faculty cannot create accounts. All new accounts require email OTP verification (6-digit, 10-min expiry, 5-attempt lockout with resend). Login accepts email or institutional ID + password and returns a JWT access token (15 min) + rotated refresh token (7 days) with family-based reuse detection. Logout revokes the refresh token immediately; the access token expires naturally within its remaining lifetime (≤ 15 min).

## Technical Context

**Language/Version**: .NET 10 / C# 13

**Primary Dependencies**:
- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT access token validation (NEW)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (existing) — password hashing, user management
- `Npgsql.EntityFrameworkCore.PostgreSQL` (existing) — PostgreSQL persistence
- `MailKit` — SMTP email for OTP delivery (NEW)
- `xUnit` + `Moq` (existing) — unit tests

**Storage**: PostgreSQL via EF Core / Npgsql

**Testing**: xUnit + Moq, unit tests only — integration tests PROHIBITED per constitution V

**Target Platform**: Linux/Windows server (ASP.NET Core Web API, .NET 10)

**Project Type**: Web service (REST API)

**Performance Goals**:
- Account creation + OTP dispatch < 5 s (SC-001)
- OTP email delivery < 60 s (SC-002)
- Login with valid credentials < 2 s (SC-003)

**Constraints**:
- JWT only; no external identity providers (constitution III)
- Unit tests only (constitution V)
- Clean Architecture 4-layer boundary (constitution I)
- Access token: 15 min; refresh token: 7 days, rotated on every use
- OTP: 6-digit, 10-min expiry, 5-attempt lockout (FR-013, FR-015a)
- Zero warnings on `dotnet build`

## Constitution Check

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Clean Architecture — 4 layers, inward dependencies | ✅ PASS | Controllers → Application services → Infrastructure repos; no layer violations |
| I | Thin controllers, business logic in Application | ✅ PASS | `AccountService`/`AuthService` own all logic |
| I | Repository pattern for domain data | ✅ PASS | `IOtpRepository`, `IRefreshTokenRepository` in Application; impls in Infrastructure |
| II | EF Core only (no Dapper, ADO.NET) | ✅ PASS | All DB access via AppDbContext |
| II | IIdentityService for user writes | ✅ PASS | All `UserManager` calls go through `IdentityService` |
| II | No UserRepository | ✅ PASS | Using `IIdentityService` exclusively |
| III | JWT only, no external IdP | ✅ PASS | `JwtBearer` + custom `TokenService` |
| III | `AddIdentityCore` (not `AddIdentity`) | ✅ PASS | Already configured in Infrastructure DI |
| III | `[Authorize]`/`[AllowAnonymous]` on all endpoints | ✅ PASS | Applied to all new controllers |
| IV | OpenAPI — XML docs + `[ProducesResponseType]` | ✅ PASS | Applied to every endpoint and model |
| V | Unit tests only (xUnit + Moq) | ✅ PASS | No integration tests; all external deps mocked |
| VI | `AppException` subtypes only | ✅ PASS | Adding `ForbiddenException` (403) and `UnauthorizedException` (401) |
| VI | RFC 7807 Problem Details | ✅ PASS | `GlobalExceptionHandler` already handles this |
| VII | Async/await everywhere | ✅ PASS | All I/O operations are async |
| VIII | `ILogger<T>` only | ✅ PASS | No Console.WriteLine anywhere |

**No violations. All gates passed.**

## Project Structure

### Documentation (this feature)

```text
specs/001-user-auth/
├── plan.md              # This file
├── research.md          # Phase 0: technology decisions and rationale
├── data-model.md        # Phase 1: entity definitions and relationships
├── quickstart.md        # Phase 1: dev setup and manual testing guide
├── contracts/
│   ├── register.md      # Phase 1: /api/register endpoint contracts
│   └── auth.md          # Phase 1: /api/auth endpoint contracts
└── tasks.md             # Phase 2 output (/speckit-tasks — not yet generated)
```

### Source Code (repository root)

```text
src/
├── Lumen.Domain/
│   ├── Enums/
│   │   └── UserRole.cs                           (NEW — Admin, Librarian, Student, Faculty)
│   └── Entities/
│       ├── Student.cs                     (NEW — plain POCO, no EF Core attributes)
│       ├── Faculty.cs                     (NEW)
│       ├── Librarian.cs                   (NEW)
│       └── OtpRecord.cs                          (NEW)
│
├── Lumen.Application/
│   ├── Common/
│   │   ├── Constants/
│   │   │   └── Roles.cs                          (NEW — Admin, Librarian, Student, Faculty via nameof(UserRole.*))
│   │   ├── Exceptions/
│   │   │   ├── AppException.cs                   (existing)
│   │   │   ├── ConflictException.cs              (existing)
│   │   │   ├── NotFoundException.cs              (existing)
│   │   │   ├── ValidationException.cs            (existing)
│   │   │   ├── ForbiddenException.cs             (NEW — 403)
│   │   │   └── UnauthorizedException.cs          (NEW — 401)
│   │   └── Interfaces/
│   │       ├── IIdentityService.cs               (EXTEND — add CreateUserAsync(email, firstName, lastName, password, role, institutionalId), FindUserByEmailAsync, FindUserByInstitutionalIdAsync, CheckPasswordAsync, GetUserRoleAsync, SetVerifiedAsync; CreateUserAsync atomically creates ApplicationUser + profile entity in Infrastructure so Application never touches AppDbContext)
│   │       ├── IEmailService.cs                  (NEW — SendOtpEmailAsync)
│   │       ├── ITokenService.cs                  (NEW — GenerateAccessToken(string userId, string email, string role): string; GenerateRefreshToken(): string — NO ApplicationUser reference; Application layer must stay free of Infrastructure types)
│   │       ├── IOtpRepository.cs                 (NEW)
│   │       └── IRefreshTokenRepository.cs        (NEW)
│   └── Features/
│       └── Auth/
│           ├── DTOs/
│           │   ├── CreateAccountRequest.cs
│           │   ├── CreateAccountResponse.cs
│           │   ├── LoginRequest.cs
│           │   ├── LoginResponse.cs           (no refreshToken field — delivered via HttpOnly cookie)
│           │   ├── VerifyOtpRequest.cs
│           │   └── ResendOtpRequest.cs
│           ├── Interfaces/
│           │   ├── IRegisterService.cs
│           │   └── IAuthService.cs
│           └── Services/
│               ├── RegisterService.cs
│               └── AuthService.cs
│
├── Lumen.Infrastructure/
│   ├── Data/
│   │   ├── AppDbContext.cs                       (EXTEND — add DbSets for new entities)
│   │   └── Configurations/
│   │       ├── StudentConfiguration.cs    (NEW — EF Core FK + index config)
│   │       ├── FacultyConfiguration.cs    (NEW)
│   │       ├── LibrarianConfiguration.cs  (NEW)
│   │       ├── OtpRecordConfiguration.cs         (NEW)
│   │       └── RefreshTokenConfiguration.cs      (NEW)
│   ├── Entities/
│   │   └── RefreshToken.cs                       (NEW — auth mechanism, not business domain)
│   ├── Identity/
│   │   ├── ApplicationUser.cs                    (EXTEND — add FirstName, LastName, Role, IsVerified, CreatedAt)
│   │   ├── AdminSeeder.cs                        (existing)
│   │   ├── AdminSeedOptions.cs                   (existing)
│   │   └── IdentityService.cs                    (EXTEND — implement new IIdentityService methods)
│   ├── Repositories/
│   │   ├── OtpRepository.cs
│   │   └── RefreshTokenRepository.cs
│   ├── Services/
│   │   ├── SmtpEmailService.cs
│   │   └── TokenService.cs
│   ├── Migrations/
│   │   └── (new migration: AddAuthEntities)
│   └── DependencyInjection.cs                    (EXTEND — register JWT auth, MailKit, new services)
│
└── Lumen.API/
    ├── Controllers/
    │   ├── RegisterController.cs                 (NEW — POST /api/register, /verify, /resend-otp)
    │   └── AuthController.cs                     (NEW — POST /api/auth/login, /refresh, /logout)
    └── Program.cs                                (EXTEND — add JWT auth middleware, OpenAPI XML docs)

tests/
└── Lumen.Tests/
    └── Features/
        └── Auth/
            ├── RegisterServiceTests.cs
            └── AuthServiceTests.cs
```

**Structure Decision**: Single .NET solution, 4 existing projects. New files slot into the established hierarchy — no new projects. The Domain layer gains a `UserRole` enum only (pure, no dependencies). All EF Core entities stay in Infrastructure since they reference Identity types.

## Complexity Tracking

No constitution violations requiring justification.
