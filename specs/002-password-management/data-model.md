# Data Model: Password Management & Session Control

**Feature**: 002-password-management  
**Date**: 2026-05-28

---

## Entities

### OtpRecord (modified)

Lives in `Lumen.Domain/Entities/OtpRecord.cs`.

Current shape plus two additions and one factory update:

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `Guid.NewGuid()` default |
| `UserId` | `string` | FK to `AspNetUsers.Id` |
| `CodeHash` | `string` | SHA-256 hex of raw OTP |
| `IssuedAt` | `DateTimeOffset` | UTC timestamp |
| `ExpiresAt` | `DateTimeOffset` | UTC; 10 minutes after `IssuedAt` |
| `IsInvalidated` | `bool` | True when superseded or locked |
| `FailedAttempts` | `int` | Incremented on wrong OTP |
| `LockedUntil` | `DateTimeOffset?` | Set when `FailedAttempts >= MaxAttempts` |
| **`Purpose`** *(NEW)* | `OtpPurpose` | `Registration` (0) or `PasswordReset` (1) |
| **`IsUsed`** *(NEW)* | `bool` | True only when consumed by successful verification |

**New domain methods**:
- `Consume()` — sets `IsUsed = true`, `IsInvalidated = true`. Called only on successful OTP verification.
- `OtpRecord.Create(...)` — gains a `purpose` parameter.

**`OtpPurpose` enum** — new file `Lumen.Domain/Enums/OtpPurpose.cs`:
```
Registration = 0
PasswordReset = 1
```

**EF Configuration changes** (`OtpRecordConfiguration.cs`):
- `Purpose`: `HasConversion<int>()`, default = 0 (migration will backfill existing rows as `Registration`)
- `IsUsed`: `HasDefaultValue(false)`

---

### RefreshToken (no change)

Session revocation for this feature uses the existing `RevokeAllForUserAsync(string userId)` on `IRefreshTokenRepository`. No schema change required.

---

### ApplicationUser (no change)

`UserManager.ChangePasswordAsync` / `ResetPasswordAsync` automatically refreshes the `SecurityStamp`. This is the token-version signal baked into Identity. No additional column needed.

---

## Repository Interface Changes

### IOtpRepository (modified)

New or updated signatures — all existing callers (`RegisterService`) must pass `OtpPurpose.Registration`:

```
// Updated (add purpose param):
Task<OtpRecord?> GetActiveOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
Task<DateTimeOffset?> GetLastIssuedAtAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
Task InvalidateAllForUserAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)

// New:
Task<OtpRecord?> GetLatestOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct = default)
Task DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct = default)
```

`GetLatestOtpAsync` returns the most recent OTP for (userId, purpose) regardless of invalidated/expired status. Used only by the reset-password flow to produce distinct error codes.

`DeleteOlderThanAsync` deletes all rows where `IssuedAt < threshold`. Used by `OtpCleanupService` with `utcNow - 24h`.

### IIdentityService (modified)

Three new methods:

```
// Validates password against Identity rules; throws ValidationException if invalid; no state change.
Task ValidatePasswordAsync(string password, CancellationToken ct = default)

// Wraps UserManager.ChangePasswordAsync; throws ValidationException on wrong current password or complexity failure.
Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default)

// Wraps GeneratePasswordResetTokenAsync + ResetPasswordAsync; throws ValidationException on complexity failure.
Task ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
```

---

## New Application-Layer Service

### IPasswordService / PasswordService

**Location**: `Lumen.Application/Services/Auth/`

```
Task<(LoginResponse Response, string RawRefreshToken)> ChangePasswordAsync(
    string userId, ChangePasswordRequest request, CancellationToken ct = default)

Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)

Task ResendResetOtpAsync(ResendResetOtpRequest request, CancellationToken ct = default)

Task<(LoginResponse Response, string RawRefreshToken)> ResetPasswordAsync(
    ResetPasswordRequest request, CancellationToken ct = default)

Task LogoutAllDevicesAsync(string userId, CancellationToken ct = default)
```

**Dependencies injected**:
- `IIdentityService`
- `IOtpRepository`
- `IRefreshTokenRepository`
- `IEmailService`
- `ITokenService`
- `IOptions<JwtSettings>`
- `ILogger<PasswordService>`

---

## New DTOs

All in `Lumen.Application/DTOs/Auth/` using `required` + `init` properties with data annotations.

| DTO | Fields |
|---|---|
| `ChangePasswordRequest` | `CurrentPassword`, `NewPassword` |
| `ForgotPasswordRequest` | `Email` |
| `ResendResetOtpRequest` | `Email` |
| `ResetPasswordRequest` | `Email`, `Otp`, `NewPassword` |

`ChangePasswordAsync` returns `LoginResponse` (reuse existing DTO from `Lumen.Application.DTOs.Auth`).

---

## Background Service

### OtpCleanupService

**Location**: `Lumen.Infrastructure/Services/OtpCleanupService.cs`

Extends `BackgroundService`. Runs every 1 hour. Calls `IOtpRepository.DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddHours(-24))`. Uses `IServiceScopeFactory` to resolve scoped dependencies (same pattern as `RefreshTokenCleanupService`).

---

## Files Modified

| File | Change |
|---|---|
| `Lumen.Domain/Entities/OtpRecord.cs` | Add `Purpose`, `IsUsed`, `Consume()` |
| `Lumen.Domain/Enums/OtpPurpose.cs` | **NEW** |
| `Lumen.Application/Common/Interfaces/IIdentityService.cs` | Add 3 methods |
| `Lumen.Application/Common/Interfaces/IOtpRepository.cs` | Update + add 2 methods |
| `Lumen.Application/DTOs/Auth/*.cs` | **NEW** (4 files) |
| `Lumen.Application/Services/Auth/IPasswordService.cs` | **NEW** |
| `Lumen.Application/Services/Auth/PasswordService.cs` | **NEW** |
| `Lumen.Infrastructure/Identity/IdentityService.cs` | Implement 3 new IIdentityService methods |
| `Lumen.Infrastructure/Repositories/OtpRepository.cs` | Implement updated + new methods |
| `Lumen.Infrastructure/Data/Configurations/OtpRecordConfiguration.cs` | Add `Purpose`, `IsUsed` config |
| `Lumen.Infrastructure/Services/OtpCleanupService.cs` | **NEW** |
| `Lumen.Infrastructure/DependencyInjection.cs` | Register `PasswordService`, `OtpCleanupService` |
| `Lumen.API/Controllers/AuthController.cs` | MODIFY: add 5 endpoints + `IPasswordService` injection |
| `Lumen.API/Program.cs` | Configure rate limiting |
| `Lumen.Application/Services/Auth/RegisterService.cs` | Pass `OtpPurpose.Registration` to repo calls |
