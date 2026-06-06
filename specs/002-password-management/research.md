# Research: Password Management & Session Control

**Feature**: 002-password-management  
**Date**: 2026-05-28

## Decision 1 — Session invalidation for change-password

**Decision**: Revoke all refresh tokens for the user via `RevokeAllForUserAsync`, then issue a new access + refresh token pair, returning it in the response.

**Rationale**: The simplest approach that satisfies "keep current session active while invalidating all others." By revoking all tokens and issuing fresh ones in the same response, the caller's session is seamlessly re-established. The alternative (revoke all except current) would require the service layer to know which refresh token is "current," but that token lives in an HttpOnly cookie readable only at the controller layer — passing it down to the service layer would violate separation of concerns.

**Alternatives considered**:
- Keep current refresh token, revoke others: rejected — the current token is only available as an HttpOnly cookie at the controller, threading it through the service layer breaks layering.
- Revoke all, do not issue new tokens: rejected — user would be logged out of their current device, violating the spec.

---

## Decision 2 — Password validation before OTP consumption (FR-011)

**Decision**: Add `ValidatePasswordAsync(string password, CancellationToken ct)` to `IIdentityService`. This method iterates `UserManager.PasswordValidators` with a transient dummy user and throws `ValidationException` without making any state change.

**Rationale**: `UserManager.ResetPasswordAsync` validates and changes in one call. If we call it after consuming the OTP and it fails, the OTP is gone. Pre-validating separately ensures the OTP is consumed only when the full operation will succeed.

**Alternatives considered**:
- Validate via data annotations on DTO: rejected — data annotations cannot replicate Identity's runtime-configured password rules (minimum length, required chars, etc.) which come from `IdentityOptions.Password`.
- Call `ResetPasswordAsync` speculatively, then consume OTP on success: rejected — this would change the password before the OTP is consumed; a crash between the two steps would leave the password changed but the OTP still active.

---

## Decision 3 — New `IIdentityService` methods for password operations

**Decision**: Add three methods to `IIdentityService`:
- `ValidatePasswordAsync(string password, CancellationToken ct)` — validates without side effects
- `ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct)` — wraps `UserManager.ChangePasswordAsync`
- `ResetPasswordAsync(string userId, string newPassword, CancellationToken ct)` — wraps `UserManager.GeneratePasswordResetTokenAsync` + `UserManager.ResetPasswordAsync` (generates and immediately uses an internal Identity token so the operation goes through all built-in validators and hooks)

**Rationale**: Change-password and reset-password have different trust models (current password known vs OTP-verified) so they map to different Identity operations. Both must go through `UserManager` to ensure password hashing and security stamp updates are handled correctly.

---

## Decision 4 — OTP purpose discrimination

**Decision**: Add `OtpPurpose` enum (`Registration`, `PasswordReset`) to `Lumen.Domain/Enums/` and a `Purpose` field to `OtpRecord`. Update `IOtpRepository` methods to accept a `purpose` parameter.

**Rationale**: Registration OTPs and password-reset OTPs share the same table. Without a discriminator, a password-reset OTP lookup could theoretically collide with a registration OTP if both existed simultaneously. The discriminator also makes the intent of each OTP explicit and is a clean solution for future audit reporting.

**Alternatives considered**:
- Rely on `IsVerified` status to discriminate implicitly: rejected — fragile implicit contract; a verified user could still have a residual registration OTP row.
- Separate `PasswordResetOtp` table: rejected — duplication of structure for the same data shape.

---

## Decision 5 — Distinct OTP error codes (FR-008a)

**Decision**: Add `bool IsUsed` property and `void Consume()` method to `OtpRecord`. Add `GetLatestOtpAsync(string userId, OtpPurpose purpose)` to `IOtpRepository` that returns the most recent OTP regardless of active/invalidated status.

**Rationale**: Current `GetActiveOtpAsync` returns `null` for all non-active OTPs, making it impossible to distinguish "expired", "already used", and "max attempts exceeded." The new method returns any state so the caller can inspect `IsUsed`, `FailedAttempts`, `IsInvalidated`, and `ExpiresAt` to produce the correct error code.

**State → error code mapping**:
| OTP state | Error code |
|---|---|
| `GetLatestOtpAsync` returns null | `no_otp_requested` |
| `IsInvalidated && FailedAttempts >= 5` | `max_attempts_exceeded` |
| `IsUsed` | `otp_already_used` |
| `ExpiresAt <= now` | `otp_expired` |
| Hash mismatch | `invalid_otp` |
| Hash matches, password invalid | 400 (OTP not consumed) |
| Hash matches, password valid | success |

**`Invalidate()` vs `Consume()` semantics**:
- `Invalidate()` — superseded by a newer OTP, or locked due to max attempts
- `Consume()` — successfully used; sets both `IsUsed = true` and `IsInvalidated = true`

---

## Decision 6 — OTP cleanup (FR-015)

**Decision**: New `OtpCleanupService : BackgroundService` that runs every hour and deletes `OtpRecord` rows where `IssuedAt < utcNow - 24h`. Follows the same pattern as the existing `RefreshTokenCleanupService`. Add `DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct)` to `IOtpRepository`.

**Rationale**: Reuses the established cleanup pattern in the codebase. A simple periodic background service is sufficient for the 24-hour TTL requirement — no scheduler library needed.

**Alternatives considered**:
- PostgreSQL row TTL / pg_cron: rejected — adds operational complexity; the DB-level cleanup would bypass EF and be harder to test.
- Immediate cleanup on OTP creation: rejected — creates a consistency hole if the creation request crashes.

---

## Decision 7 — Rate limiting

**Decision**: Use ASP.NET Core's built-in `AddRateLimiter` with a fixed-window policy (3 requests per minute per IP) applied to `forgot-password` and `resend-reset-otp` endpoints via a `[EnableRateLimiting("password-reset")]` attribute. No per-user cooldown (distinct from registration which has a 1-minute per-user cooldown).

**Rationale**: IP-based rate limiting is sufficient for the spec's abuse-prevention requirement. A per-user cooldown is not specified for the password-reset flow (unlike registration). Keeping the logic at the middleware layer means the service remains stateless.

**Alternatives considered**:
- Per-user cooldown in service (like `ResendOtpAsync`): not required by spec; adds complexity; the spec only says "rate limiting."
- Third-party library (e.g. AspNetCoreRateLimit): rejected — built-in is sufficient for this use case.

---

## Decision 8 — Refresh token cookie from PasswordController

**Decision**: `PasswordController` sets the `refreshToken` HttpOnly cookie with `Path = "/api/auth"` and the same options as `AuthController`, so the token is only sent to `/api/auth/**` (where refresh/logout live).

**Rationale**: The cookie's path scope ensures the refresh token is only transmitted to the endpoints that consume it, minimising the attack surface. This must match `AuthController`'s cookie path so that `/api/auth/refresh` can read it.

---

## Decision 9 — Logout-all-devices also clears the cookie

**Decision**: `POST /api/password/logout-all-devices` calls `RevokeAllForUserAsync`, then deletes the `refreshToken` cookie from the response. The short-lived access token expires naturally.

**Rationale**: After all refresh tokens are revoked, the cookie is no longer useful and its presence would confuse the client into trying a refresh that will fail.
