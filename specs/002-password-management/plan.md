# Implementation Plan: Password Management & Session Control

**Branch**: `002-password-management` | **Date**: 2026-05-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-password-management/spec.md`

## Summary

Implement four user-facing security flows (change password, forgot password / resend OTP, reset password, logout all devices) on top of the existing JWT + refresh-token + OTP infrastructure. Session revocation is handled by revoking all refresh tokens for the user; change-password and reset-password immediately re-issue a fresh token pair so the caller's session is preserved.

## Technical Context

**Language/Version**: C# 13 / .NET 10

**Primary Dependencies**: ASP.NET Core, ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), EF Core 10 (Npgsql), MailKit, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.RateLimiting` (built-in)

**Storage**: PostgreSQL via EF Core (Npgsql).

**Testing**: xUnit + Moq — unit tests only, targeting `Lumen.Application` service logic.

**Target Platform**: Linux/Windows server (ASP.NET Core Web API)

**Project Type**: Web service (REST API)

**Performance Goals**: Password mutation operations < 500ms p95; OTP delivery within 60s (email delivery SLA)

**Constraints**: Zero-warning build; nullable references enabled; no EF/DbContext references outside Infrastructure; no integration tests

**Scale/Scope**: Per-user serialised operations; no bulk/batch concerns

## Constitution Check

*All gates pass — no violations.*

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture | `IPasswordService` in Application; `PasswordService` in Application; `IdentityService` + repos in Infrastructure; `AuthController` in API | ✅ |
| II. Data Access | All DB access through `IOtpRepository` / `IRefreshTokenRepository` / `IIdentityService`; EF-only | ✅ |
| III. Authentication | `ChangePassword` + `LogoutAllDevices` marked `[Authorize]`; other endpoints anonymous by default (no class-level `[Authorize]` on `AuthController`); JWT only; `AddIdentityCore` unchanged | ✅ |
| IV. API Documentation | All endpoints get XML doc comments + `[ProducesResponseType]` for every status code | ✅ |
| V. Unit Testing | `PasswordServiceTests` covers all service methods; xUnit + Moq; no DB/network | ✅ |
| VI. Error Handling | `AppException` subtypes; RFC 7807 via `Problem()` / `ValidationProblem()`; no empty catch blocks | ✅ |
| VII. Async & Concurrency | All I/O uses `async/await`; `ConfigureAwait(false)` in Infrastructure; no `.Result` / `.Wait()` | ✅ |
| VIII. Logging | `ILogger<PasswordService>` for audit events (`LogInformation`); structured log messages | ✅ |

## Project Structure

### Documentation (this feature)

```text
specs/002-password-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── password-api.md  # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code

```text
src/
├── Lumen.Domain/
│   ├── Entities/
│   │   └── OtpRecord.cs                  ← MODIFY: add Purpose, IsUsed, Consume()
│   └── Enums/
│       ├── OtpPurpose.cs                 ← NEW
│       ├── ProfileRole.cs
│       └── UserRole.cs
│
├── Lumen.Application/
│   ├── Common/
│   │   └── Interfaces/
│   │       ├── IEmailService.cs          ← no change
│   │       ├── IIdentityService.cs       ← MODIFY: add ValidatePasswordAsync, ChangePasswordAsync, ResetPasswordAsync
│   │       ├── IOtpRepository.cs         ← MODIFY: add purpose param + GetLatestOtpAsync, DeleteOlderThanAsync
│   │       ├── IRefreshTokenRepository.cs← no change
│   │       └── ITokenService.cs          ← no change
│   ├── DTOs/
│   │   └── Auth/                         ← NEW files added here (LoginResponse reused for token responses)
│   │       ├── ChangePasswordRequest.cs
│   │       ├── ForgotPasswordRequest.cs
│   │       ├── ResendResetOtpRequest.cs
│   │       └── ResetPasswordRequest.cs
│   ├── Services/
│   │   └── Auth/
│   │       ├── AuthService.cs            ← no change
│   │       ├── IAuthService.cs           ← no change
│   │       ├── IPasswordService.cs       ← NEW
│   │       ├── IRegisterService.cs       ← no change
│   │       ├── PasswordService.cs        ← NEW
│   │       └── RegisterService.cs        ← MODIFY: pass OtpPurpose.Registration to all IOtpRepository calls
│   └── DependencyInjection.cs            ← MODIFY: register IPasswordService → PasswordService
│
├── Lumen.Infrastructure/
│   ├── Data/
│   │   ├── AppDbContext.cs               ← no change
│   │   └── Configurations/
│   │       └── OtpRecordConfiguration.cs ← MODIFY: configure Purpose and IsUsed columns
│   ├── Identity/
│   │   └── IdentityService.cs            ← MODIFY: implement ValidatePasswordAsync, ChangePasswordAsync, ResetPasswordAsync
│   ├── Repositories/
│   │   └── OtpRepository.cs              ← MODIFY: update existing methods + implement new ones
│   ├── Services/
│   │   ├── OtpCleanupService.cs          ← NEW
│   │   ├── RefreshTokenCleanupService.cs ← no change
│   │   ├── SmtpEmailService.cs           ← no change
│   │   └── TokenService.cs               ← no change
│   └── DependencyInjection.cs            ← MODIFY: register OtpCleanupService as hosted service
│
└── Lumen.API/
    ├── Controllers/
    │   ├── AuthController.cs             ← MODIFY: add 5 new endpoints + IPasswordService injection
    │   └── RegisterController.cs         ← no change
    ├── Middleware/
    │   └── GlobalExceptionHandler.cs     ← no change
    └── Program.cs                        ← MODIFY: configure AddRateLimiter

tests/
└── Lumen.Tests/
    └── Services/
        └── PasswordServiceTests.cs       ← NEW (unit tests for PasswordService)
```

## Key Implementation Details

### PasswordService — critical flows

**ChangePasswordAsync**:
1. If `request.NewPassword == request.CurrentPassword` → throw `ConflictException`
2. `identityService.ChangePasswordAsync(userId, currentPassword, newPassword)` — throws `ValidationException` on wrong current password or complexity failure
3. `otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset)` — FR-003b
4. `refreshTokenRepository.RevokeAllForUserAsync(userId)` — FR-003a
5. Issue new access + refresh token pair; log `LogInformation("Password changed for user {UserId}", userId)`
6. Return `(LoginResponse, rawRefreshToken)`

**ResetPasswordAsync**:
1. `identityService.FindUserByEmailAsync(email)` → if null, throw `UnauthorizedException` (401 — email not found is not a user-enumeration risk here because a valid OTP is required)
2. `otpRepository.GetLatestOtpAsync(userId, OtpPurpose.PasswordReset)` → inspect state for distinct error codes (see research.md Decision 5)
3. Validate OTP hash; call `RecordFailedAttempt` on mismatch + persist
4. `identityService.ValidatePasswordAsync(newPassword)` — fails before OTP consumed (FR-011)
5. `otpRecord.Consume()` + `otpRepository.UpdateAsync(otpRecord)`
6. `identityService.ResetPasswordAsync(userId, newPassword)`
7. `otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset)` — clean up any other outstanding reset OTPs
8. `refreshTokenRepository.RevokeAllForUserAsync(userId)` — FR-012
9. Issue new token pair; log audit event
10. Return `(LoginResponse, rawRefreshToken)`

**ForgotPasswordAsync / ResendResetOtpAsync** (identical server logic; separate endpoints for distinct UX triggers and audit differentiation):
1. `identityService.FindUserByEmailAsync(email)` → if null, return silently (no-op)
2. If user not verified → return silently
3. `otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset)` — supersede any pending OTP
4. Generate OTP, hash, create `OtpRecord` with `Purpose = PasswordReset`, add
5. `emailService.SendOtpEmailAsync(email, otp)` — if this throws, propagate so caller knows to retry
6. Log with distinct message: `"Password reset OTP sent"` vs `"Password reset OTP resent"` for audit

**LogoutAllDevicesAsync**:
1. `refreshTokenRepository.RevokeAllForUserAsync(userId)`
2. `LogInformation("All sessions revoked for user {UserId}", userId)`

### AuthController (extended)

New endpoints added to the existing `AuthController`. Inject `IPasswordService` via primary constructor alongside the existing dependencies.

```
POST /change-password    → [Authorize] — extract userId from JWT claims; return 200 + new cookie
POST /forgot-password    → [EnableRateLimiting("password-reset")] — return 200 always
POST /resend-reset-otp   → [EnableRateLimiting("password-reset")] — return 200 always
POST /reset-password     → return 200 + new cookie on success; 400 with errorCode on OTP failures
POST /logout-all-devices → [Authorize] — return 204; delete refreshToken cookie
```

The `refreshToken` cookie for token-issuing endpoints: `HttpOnly = true, Secure = true, SameSite = Strict, Path = "/api/auth"` (matches `AuthController` so `/api/auth/refresh` can read it).

### Rate limiting (Program.cs)

```csharp
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("password-reset", o =>
    {
        o.PermitLimit = 3;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
// ...
app.UseRateLimiter();
```

### OtpRecordConfiguration additions
```csharp
builder.Property(o => o.Purpose).HasConversion<int>().HasDefaultValue(OtpPurpose.Registration);
builder.Property(o => o.IsUsed).HasDefaultValue(false);
```

### RegisterService update

All existing `IOtpRepository` calls gain the purpose argument:
```csharp
await otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.Registration, ct);
await otpRepository.GetActiveOtpAsync(userId, OtpPurpose.Registration, ct);
await otpRepository.GetLastIssuedAtAsync(userId, OtpPurpose.Registration, ct);
OtpRecord.Create(userId, hash, issuedAt, expiresAt, OtpPurpose.Registration)
```

## Complexity Tracking

*No constitution violations — this section intentionally left blank.*
