---

description: "Task list for Password Management & Session Control"
---

# Tasks: Password Management & Session Control

**Input**: Design documents from `/specs/002-password-management/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/password-api.md ✅, quickstart.md ✅

**Tests**: Unit tests are MANDATORY per the project constitution. `PasswordServiceTests.cs` covers all `PasswordService` methods (xUnit + Moq; no DB/network). DTOs are created first (required for test compilation), then tests are written against the stubbed service (they must fail), then the service method is implemented.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks in the same wave)
- **[Story]**: Which user story this task belongs to ([US1]–[US4])
- Setup and Foundational phases carry no story label

---

## Phase 1: Setup (Verify Starting State)

**Purpose**: Confirm the existing solution builds cleanly before any changes begin.

- [X] T001 Run `dotnet build Lumen.slnx` and confirm zero errors and zero warnings before making any changes

---

## Phase 2: Foundational — Domain & Interface Changes

**Purpose**: All domain types, interface signatures, and infrastructure implementations that every user story depends on. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: These tasks change shared contracts (OtpRecord, IOtpRepository, IIdentityService). All four user stories depend on them.

### Wave A — New types (no dependencies; all parallelizable)

- [X] T002 [P] Create `OtpPurpose` enum with values `Registration = 0` and `PasswordReset = 1` in `src/Lumen.Domain/Enums/OtpPurpose.cs`
- [X] T003 [P] Add three method signatures to `src/Lumen.Application/Common/Interfaces/IIdentityService.cs`: `ValidatePasswordAsync(string password, CancellationToken ct)`, `ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct)`, `ResetPasswordAsync(string userId, string newPassword, CancellationToken ct)`
- [X] T004 [P] Create `IPasswordService` interface in `src/Lumen.Application/Services/Auth/IPasswordService.cs` with five method signatures: `ChangePasswordAsync`, `ForgotPasswordAsync`, `ResendResetOtpAsync`, `ResetPasswordAsync`, `LogoutAllDevicesAsync` (exact signatures from data-model.md)

### Wave B — Entity and repository contract updates (require T002)

- [X] T005 [P] Modify `src/Lumen.Domain/Entities/OtpRecord.cs`: add `Purpose` property (`OtpPurpose`, default `Registration`), add `IsUsed` property (`bool`, default `false`), add `Consume()` method that sets `IsUsed = true` and `IsInvalidated = true`; update `OtpRecord.Create(...)` factory to accept a `purpose` parameter
- [X] T006 [P] Update `src/Lumen.Application/Common/Interfaces/IOtpRepository.cs`: add `OtpPurpose purpose` parameter to `GetActiveOtpAsync`, `GetLastIssuedAtAsync`, and `InvalidateAllForUserAsync`; add new signature `GetLatestOtpAsync(string userId, OtpPurpose purpose, CancellationToken ct)` (returns the most recent OTP regardless of state); add new signature `DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct)`

### Wave C — Infrastructure implementations (require Wave B and T003)

- [X] T007 [P] Implement the three new methods on `src/Lumen.Infrastructure/Identity/IdentityService.cs`: `ValidatePasswordAsync` (iterates `UserManager.PasswordValidators` with a transient dummy user, no state change), `ChangePasswordAsync` (wraps `UserManager.ChangePasswordAsync`), `ResetPasswordAsync` (wraps `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`); all use `ConfigureAwait(false)`
- [X] T008 [P] Update EF configuration in `src/Lumen.Infrastructure/Data/Configurations/OtpRecordConfiguration.cs`: configure `Purpose` with `HasConversion<int>()` and `HasDefaultValue(OtpPurpose.Registration)`; configure `IsUsed` with `HasDefaultValue(false)`
- [X] T009 [P] Update `src/Lumen.Infrastructure/Repositories/OtpRepository.cs`: add the `purpose` parameter to `GetActiveOtpAsync`, `GetLastIssuedAtAsync`, and `InvalidateAllForUserAsync` (filter by purpose); implement `GetLatestOtpAsync` (returns single most-recent row for userId+purpose, ordered by `IssuedAt` descending, no active-only filter); implement `DeleteOlderThanAsync` (delete where `IssuedAt < threshold`); use `ConfigureAwait(false)` on all async calls
- [X] T010 [P] Update `src/Lumen.Application/Services/Auth/RegisterService.cs`: pass `OtpPurpose.Registration` as the purpose argument to all five `IOtpRepository` calls (`InvalidateAllForUserAsync`, `GetActiveOtpAsync`, `GetLastIssuedAtAsync`, and the `OtpRecord.Create(...)` factory call)

### Wave D — PasswordService skeleton (requires T004 and Wave B interfaces)

- [X] T011 Create `src/Lumen.Application/Services/Auth/PasswordService.cs`: implement `IPasswordService`; inject all seven dependencies via primary constructor (`IIdentityService`, `IOtpRepository`, `IRefreshTokenRepository`, `IEmailService`, `ITokenService`, `IOptions<JwtSettings>`, `ILogger<PasswordService>`); add five method stubs that `throw new NotImplementedException()` so the file compiles and tests can be written against it

**Checkpoint**: Foundation ready — run `dotnet build Lumen.slnx` (zero errors). All four user story phases can now proceed.

---

## Phase 3: User Story 1 — Change Password (Priority: P1) 🎯 MVP

**Goal**: Authenticated users can change their password. The current session is preserved; all other sessions are revoked. A fresh token pair is returned.

**Independent Test**: Log in, call `POST /api/auth/change-password` with the correct current password and a valid new password → 200 with new tokens; old password login → 401; tokens from other sessions → rejected.

### Supporting Types for User Story 1

- [X] T012 [P] [US1] Create `ChangePasswordRequest.cs` DTO in `src/Lumen.Application/DTOs/Auth/ChangePasswordRequest.cs` with `required string CurrentPassword` and `required string NewPassword` properties using `init` accessors, each annotation on its own line above the property

### Tests for User Story 1 (write after T012; must fail before T014) ⚠️

- [X] T013 [US1] Create `tests/Lumen.Tests/Services/PasswordServiceTests.cs` with unit tests for `PasswordService.ChangePasswordAsync` using `MethodName_Scenario_ExpectedBehavior` naming: `ChangePasswordAsync_Success_ReturnsNewTokensAndRevokesAllSessions`, `ChangePasswordAsync_WrongCurrentPassword_ThrowsValidationException`, `ChangePasswordAsync_ComplexityFailure_ThrowsValidationException`, `ChangePasswordAsync_NewPasswordSameAsCurrent_ThrowsConflictException`; verify `InvalidateAllForUserAsync` called with `PasswordReset`, `RevokeAllForUserAsync` called, `LogInformation` called

### Implementation for User Story 1

- [X] T014 [US1] Implement `PasswordService.ChangePasswordAsync` in `src/Lumen.Application/Services/Auth/PasswordService.cs` following the exact five-step flow from plan.md: (1) throw `ConflictException` if `NewPassword == CurrentPassword`; (2) call `identityService.ChangePasswordAsync`; (3) `otpRepository.InvalidateAllForUserAsync(userId, OtpPurpose.PasswordReset)`; (4) `refreshTokenRepository.RevokeAllForUserAsync(userId)`; (5) issue new token pair via `ITokenService`, log audit event, return `(LoginResponse, rawRefreshToken)`
- [X] T015 [US1] Add `IPasswordService` injection to `AuthController.cs` primary constructor in `src/Lumen.API/Controllers/AuthController.cs`; add `POST change-password` action with return type `Task<ActionResult<LoginResponse>>`: `[Authorize]`, extract `userId` from JWT sub claim, call `ChangePasswordAsync`, set `refreshToken` HttpOnly cookie (`Secure=true, SameSite=Strict, Path="/api/auth"`), return `Ok(response)`; add XML doc comments and `[ProducesResponseType<LoginResponse>(200)]`, `[ProducesResponseType(400)]`, `[ProducesResponseType(401)]`
- [X] T016 [US1] Register `IPasswordService → PasswordService` with scoped lifetime in `src/Lumen.Application/DependencyInjection.cs`

**Checkpoint**: US1 fully functional — run `dotnet test --filter "FullyQualifiedName~PasswordServiceTests"` (all US1 tests pass); manual test via quickstart.md Story 1 curl commands.

---

## Phase 4: User Story 2 — Forgot Password / Resend OTP (Priority: P2)

**Goal**: Unauthenticated users can trigger a password-reset OTP via email. The resend path invalidates the previous OTP and issues a fresh one. Both endpoints always return 200 to prevent user enumeration. Rate limiting protects against abuse.

**Independent Test**: Call `POST /api/auth/forgot-password` with a registered email → 200 + OTP in inbox; call again (resend) → new OTP in inbox, old OTP rejected; 4th request in 1 minute → 429.

### Supporting Types for User Story 2

- [X] T017 [P] [US2] Create `ForgotPasswordRequest.cs` in `src/Lumen.Application/DTOs/Auth/ForgotPasswordRequest.cs` with `required string Email` and `[EmailAddress]` annotation; create `ResendResetOtpRequest.cs` in `src/Lumen.Application/DTOs/Auth/ResendResetOtpRequest.cs` with the same shape

### Tests for User Story 2 (write after T017; must fail before T020–T021) ⚠️

- [X] T018 [US2] Add unit tests for `PasswordService.ForgotPasswordAsync` to `tests/Lumen.Tests/Services/PasswordServiceTests.cs` using `MethodName_Scenario_ExpectedBehavior` naming: `ForgotPasswordAsync_RegisteredEmail_InvalidatesOldOtpAndSendsNew`, `ForgotPasswordAsync_UnregisteredEmail_ReturnsWithNoSideEffects`, `ForgotPasswordAsync_UnverifiedUser_ReturnsWithNoSideEffects`, `ForgotPasswordAsync_EmailSendFailure_PropagatesException`; verify `LogInformation` message contains "sent"
- [X] T019 [US2] Add unit tests for `PasswordService.ResendResetOtpAsync` to `tests/Lumen.Tests/Services/PasswordServiceTests.cs` using `MethodName_Scenario_ExpectedBehavior` naming: same scenarios as T018 but verify `LogInformation` message contains "resent"

### Implementation for User Story 2

- [X] T020 [US2] Implement `PasswordService.ForgotPasswordAsync` in `src/Lumen.Application/Services/Auth/PasswordService.cs` following the six-step flow from plan.md: (1) `FindUserByEmailAsync` — if null, return silently; (2) if user unverified, return silently; (3) `InvalidateAllForUserAsync(userId, PasswordReset)`; (4) generate OTP + hash + create `OtpRecord` with `Purpose = PasswordReset` and 10-minute expiry; (5) `emailService.SendOtpEmailAsync` — propagate failure; (6) log `"Password reset OTP sent"` audit event
- [X] T021 [US2] Implement `PasswordService.ResendResetOtpAsync` in `src/Lumen.Application/Services/Auth/PasswordService.cs` — identical flow to `ForgotPasswordAsync` but log message says `"Password reset OTP resent"`
- [X] T022 [US2] Add two endpoints to `src/Lumen.API/Controllers/AuthController.cs` with return type `Task<ActionResult>`: `POST forgot-password` (`[HttpPost("forgot-password")]`, `[EnableRateLimiting("password-reset")]`, calls `ForgotPasswordAsync`, returns `Ok(new { message = "A reset code has been sent." })`) and `POST resend-reset-otp` (same shape); add XML doc comments and `[ProducesResponseType(200)]`, `[ProducesResponseType(429)]` on both
- [X] T023 [US2] Configure rate limiting in `src/Lumen.API/Program.cs`: call `builder.Services.AddRateLimiter` with a `"password-reset"` fixed-window limiter (`PermitLimit = 3`, `Window = 1 min`, `QueueLimit = 0`, `RejectionStatusCode = 429`); add `app.UseRateLimiter()` before `app.MapControllers()`

**Checkpoint**: US2 fully functional — run US2 tests; manual test via quickstart.md Story 2 curl commands including the rate-limit loop test.

---

## Phase 5: User Story 3 — Reset Password (Priority: P3)

**Goal**: Users can reset their password by providing their email, the OTP they received, and a new password in a single atomic request. All existing sessions are revoked and a new JWT is issued immediately. Distinct error codes are returned for each OTP failure mode.

**Independent Test**: Obtain OTP from US2, call `POST /api/auth/reset-password` → 200 + new tokens; same OTP again → 400 `otp_already_used`; wrong OTP 5 times → 400 `max_attempts_exceeded`; expired OTP → 400 `otp_expired`; bad password (OTP still valid) → 400 (no errorCode); all prior tokens → rejected.

### Supporting Types for User Story 3

- [X] T024 [P] [US3] Create `ResetPasswordRequest.cs` DTO in `src/Lumen.Application/DTOs/Auth/ResetPasswordRequest.cs` with `required string Email`, `required string Otp`, `required string NewPassword`; each data annotation (`[EmailAddress]`, `[Required]`) on its own line above the property

### Tests for User Story 3 (write after T024; must fail before T026) ⚠️

- [X] T025 [US3] Add unit tests for `PasswordService.ResetPasswordAsync` to `tests/Lumen.Tests/Services/PasswordServiceTests.cs` using `MethodName_Scenario_ExpectedBehavior` naming covering all seven scenarios from spec.md: `ResetPasswordAsync_ValidOtpAndPassword_ConsumesOtpRevokesSessionsReturnsTokens`; `ResetPasswordAsync_NoOtpRequested_ThrowsUnauthorizedException`; `ResetPasswordAsync_MaxAttemptsExceeded_ThrowsWithErrorCode`; `ResetPasswordAsync_OtpAlreadyUsed_ThrowsWithErrorCode`; `ResetPasswordAsync_ExpiredOtp_ThrowsWithErrorCode`; `ResetPasswordAsync_InvalidOtp_RecordsFailedAttemptAndThrows`; `ResetPasswordAsync_InvalidPassword_DoesNotConsumeOtp`

### Implementation for User Story 3

- [X] T026 [US3] Implement `PasswordService.ResetPasswordAsync` in `src/Lumen.Application/Services/Auth/PasswordService.cs` following the exact ten-step flow from plan.md: (1) find user by email — 401 if null; (2) `GetLatestOtpAsync(userId, PasswordReset)` — inspect state for distinct error codes (research.md Decision 5 state table); (3) validate hash — call `RecordFailedAttempt` on mismatch + `UpdateAsync` + throw with `invalid_otp`; (4) `ValidatePasswordAsync(newPassword)` — throws before OTP consumed (FR-011); (5) `otpRecord.Consume()` + `UpdateAsync`; (6) `identityService.ResetPasswordAsync(userId, newPassword)`; (7) `InvalidateAllForUserAsync(userId, PasswordReset)`; (8) `RevokeAllForUserAsync(userId)`; (9) issue new token pair; (10) log audit event; return `(LoginResponse, rawRefreshToken)`
- [X] T027 [US3] Add `POST reset-password` endpoint to `src/Lumen.API/Controllers/AuthController.cs` with return type `Task<ActionResult<LoginResponse>>`: calls `ResetPasswordAsync`, sets refreshToken cookie on success, returns `Ok(response)`; maps `AppException` subtypes to Problem Details with `errorCode` extension field per contracts/password-api.md; add `[ProducesResponseType<LoginResponse>(200)]`, `[ProducesResponseType(400)]`, `[ProducesResponseType(401)]`

**Checkpoint**: US3 fully functional — run US3 tests; manual test via quickstart.md Story 2+3 curl commands.

---

## Phase 6: User Story 4 — Logout All Devices (Priority: P4)

**Goal**: Authenticated users can revoke all active sessions in one action. The refreshToken cookie is cleared and any further use of existing tokens is rejected.

**Independent Test**: Authenticate on two "devices" (two token pairs), call `POST /api/auth/logout-all-devices` with token A → 204; attempt refresh with either cookie → 401.

### Tests for User Story 4 (write first — must fail before T029) ⚠️

- [X] T028 [P] [US4] Add unit tests for `PasswordService.LogoutAllDevicesAsync` to `tests/Lumen.Tests/Services/PasswordServiceTests.cs` using `MethodName_Scenario_ExpectedBehavior` naming: `LogoutAllDevicesAsync_ValidUser_RevokesAllSessions` (verifies `RevokeAllForUserAsync(userId)` called once); `LogoutAllDevicesAsync_ValidUser_LogsAuditEventWithUserId` (verifies `LogInformation` called with userId); `LogoutAllDevicesAsync_ValidUser_IssuesNoNewTokens` (verifies no `ITokenService` calls)

### Implementation for User Story 4

- [X] T029 [US4] Implement `PasswordService.LogoutAllDevicesAsync` in `src/Lumen.Application/Services/Auth/PasswordService.cs`: call `refreshTokenRepository.RevokeAllForUserAsync(userId)`; log `LogInformation("All sessions revoked for user {UserId}", userId)`
- [X] T030 [US4] Add `POST logout-all-devices` endpoint to `src/Lumen.API/Controllers/AuthController.cs` with return type `Task<ActionResult>`: `[Authorize]`, extract `userId` from JWT claims, call `LogoutAllDevicesAsync`, delete `refreshToken` cookie (set expired `CookieOptions` with `Path = "/api/auth"`), return `NoContent()` (204); add `[ProducesResponseType(204)]`, `[ProducesResponseType(401)]`

**Checkpoint**: US4 fully functional — run all `PasswordServiceTests`; manual test via quickstart.md Story 4 curl commands.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Background cleanup service, DI wiring for infrastructure, EF migration, and final build/test validation.

- [X] T031 [P] Create `src/Lumen.Infrastructure/Services/OtpCleanupService.cs`: extend `BackgroundService`; resolve `IOtpRepository` via `IServiceScopeFactory` (same pattern as `RefreshTokenCleanupService`); call `DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddHours(-24))` every 1 hour; use `ConfigureAwait(false)` throughout
- [X] T032 Register `OtpCleanupService` as a hosted service in `src/Lumen.Infrastructure/DependencyInjection.cs` using `services.AddHostedService<OtpCleanupService>()`
- [X] T033 Create EF migration `AddOtpPurposeAndIsUsed`: run `dotnet ef migrations add AddOtpPurposeAndIsUsed --project src/Lumen.Infrastructure --startup-project src/Lumen.API`; review the generated migration and confirm it adds `Purpose int NOT NULL DEFAULT 0` and `IsUsed boolean NOT NULL DEFAULT false` to the `OtpRecords` table
- [X] T034 Run `dotnet test` (full suite, zero failures) then `dotnet build Lumen.slnx` (zero warnings) to confirm the feature is complete

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Phase 2
- **US2 (Phase 4)**: Depends on Phase 2; may follow US1 but is independently testable
- **US3 (Phase 5)**: Depends on Phase 2 and logically on Phase 4 (needs OTPs to exist)
- **US4 (Phase 6)**: Depends on Phase 2 only — fully independent of US1–US3
- **Polish (Phase 7)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (Change Password)**: Independent after Foundational phase
- **US2 (Forgot Password / Resend OTP)**: Independent after Foundational phase
- **US3 (Reset Password)**: Logically depends on US2 (needs OTPs to test against); code is independent
- **US4 (Logout All Devices)**: Fully independent after Foundational phase

### Within Each User Story

1. DTO created first (required for test compilation)
2. Tests written next (must compile but fail — service stubs throw `NotImplementedException`)
3. Service method implementation (tests should now pass)
4. Controller endpoint wired up
5. DI registration enables end-to-end testing

### Parallel Opportunities

Within Phase 2, three parallel waves exist:
- **Wave A**: T002, T003, T004 — all independent new types
- **Wave B**: T005, T006, T007 — depend only on Wave A; all modify distinct files
- **Wave C**: T008, T009, T010 — depend only on Wave B; all modify distinct files

Within each user story phase, the DTO task [P] can start as soon as the previous phase is done. Test and implementation tasks are sequential after the DTO.

---

## Parallel Example: User Story 1

```bash
# DTO first (required for test compilation):
Task T012: Create ChangePasswordRequest.cs DTO in src/Lumen.Application/DTOs/Auth/

# Then tests (compile against the stubbed service — must fail):
Task T013: Create PasswordServiceTests.cs with ChangePasswordAsync tests

# Then implement (sequential — tests must be failing first):
Task T014: Implement PasswordService.ChangePasswordAsync
Task T015: Add change-password endpoint to AuthController.cs
Task T016: Register IPasswordService in Application DependencyInjection.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup verification
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `dotnet test --filter PasswordServiceTests` + manual curl from quickstart.md
5. Users can change their password — MVP delivered

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (Change Password) → independently testable, deployable
3. US2 (Forgot Password / Resend OTP) → independently testable, deployable
4. US3 (Reset Password) → completes the forgot/reset flow
5. US4 (Logout All Devices) → security hardening
6. Polish → background cleanup, migration, final validation

---

## Notes

- [P] tasks modify distinct files and have no dependency on incomplete tasks in the same wave
- [Story] label maps each task to its user story for traceability
- DTOs MUST be created before tests — tests reference DTO types and will not compile otherwise
- Tests MUST be written before implementation (TDD) — they should compile but fail
- All five `PasswordService` methods live in one file; tests for all stories live in one test file
- Test methods MUST follow `MethodName_Scenario_ExpectedBehavior` naming per constitution
- `AuthController.cs` is modified incrementally across US1–US4 phases; ensure `[Authorize]` on change-password and logout-all-devices; other endpoints are anonymous by default (no `[Authorize]`, no `[AllowAnonymous]` needed)
- All token-issuing endpoint action methods return `Task<ActionResult<LoginResponse>>`; the logout endpoint returns `Task<ActionResult>`
- The refreshToken cookie must use `Path = "/api/auth"` on all token-issuing endpoints to match the existing `AuthController` refresh endpoint
- Commit after each checkpoint to maintain a clean git history
- Skip the EF migration (T033) during active development; run it only when resetting the DB per project conventions
