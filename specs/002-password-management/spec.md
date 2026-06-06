# Feature Specification: Password Management & Session Control

**Feature Branch**: `002-password-management`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "I want to implement change password, forget password, reset password, logout all devices feature. For forget and reset password I want to use OTP to verify the user."

## Clarifications

### Session 2026-05-27

- Q: When a user changes their password, should other active sessions be invalidated? → A: Invalidate all other sessions, keep the current session active (Option B)
- Q: What is the default threshold for OTP failed attempts before the OTP is invalidated? → A: 5 consecutive failed attempts (Option B)
- Q: How long should expired or consumed OTP records be retained before deletion? → A: Delete after 24 hours (Option B)
- Q: After a successful password reset, should the user be logged in automatically or redirected to login? → A: Issue a new JWT automatically — user is logged in immediately (Option A)
- Q: Should the reset-password endpoint return distinct error responses for wrong OTP, expired OTP, and too many attempts? → A: Distinct error codes for each failure type (Option B)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Change Password (Priority: P1)

A logged-in user wants to update their password. They provide their current password for verification, then supply a new password. If the current password is correct and the new password meets complexity requirements, the password is updated. The user remains logged in on the current device but all other active sessions are invalidated, forcing re-authentication on any other device.

**Why this priority**: This is the most fundamental security feature — users must be able to rotate their credentials without losing access. It requires no external dependencies (email/OTP) and delivers immediate value.

**Independent Test**: Can be fully tested by authenticating with a valid account, calling the change-password endpoint with a correct current password and a valid new password, then verifying login succeeds with the new password and fails with the old one.

**Acceptance Scenarios**:

1. **Given** a logged-in user, **When** they submit their correct current password and a valid new password, **Then** the password is updated, all other active sessions are invalidated, and the current session remains valid.
2. **Given** a logged-in user, **When** they submit an incorrect current password, **Then** the request is rejected with an appropriate error and the password is not changed.
3. **Given** a logged-in user, **When** they submit a new password that does not meet complexity requirements, **Then** the request is rejected with a descriptive validation error.
4. **Given** a logged-in user, **When** they submit a new password identical to the current password, **Then** the request is rejected.
5. **Given** an unauthenticated request, **When** the change-password endpoint is called, **Then** a 401 Unauthorized response is returned.
6. **Given** a user with sessions on three devices, **When** they change their password from device A, **Then** sessions on devices B and C are invalidated but device A's session remains active.

---

### User Story 2 - Forgot Password / Resend OTP (Priority: P2)

A user who has forgotten their password enters their email address and submits a forgot-password request. The system sends a 6-digit OTP to that email and the user is taken to the OTP entry screen. If the email does not arrive, a "Resend OTP" button on that screen lets them request a fresh code — the old OTP is invalidated and a new one is sent — without leaving the screen or re-entering their email. Neither action requires the user to be logged in.

**Why this priority**: This is the entry point of the forgot/reset flow. Without it the reset cannot proceed; it must be stable before OTP verification and reset are built.

**Independent Test**: Can be fully tested by submitting a registered email and verifying an OTP delivery is triggered. The resend path is verified by requesting a second OTP and confirming the first is invalidated.

**Acceptance Scenarios**:

1. **Given** an unregistered or registered email, **When** a forgot-password request is submitted, **Then** the system responds with a generic success message (does not reveal whether the email is registered).
2. **Given** a registered email, **When** a forgot-password request is submitted, **Then** a time-limited OTP is generated and sent to that email address.
3. **Given** a user who has already received an OTP that has not expired, **When** they request a resend, **Then** the previous OTP is invalidated and a new OTP is sent to the same email.
4. **Given** a user whose OTP has expired, **When** they request a resend, **Then** a new OTP is generated and sent.
5. **Given** any email, **When** too many OTP requests (initial or resend) are submitted in a short window, **Then** subsequent requests are rate-limited.

---

### User Story 3 - Reset Password (Priority: P3)

After receiving their OTP, the user enters it alongside their email and new password in a single step. The system verifies the OTP and updates the password atomically. On success, all existing sessions for that user are invalidated and a new JWT is issued, so the user is logged in immediately without a separate login step.

**Why this priority**: This is the step that actually restores access and completes the forgot-password flow. It depends on Story 2.

**Independent Test**: Can be fully tested by obtaining a valid OTP from Story 2, submitting it together with a new password, then verifying login succeeds with the new password, fails with the old one, and all prior tokens are rejected.

**Acceptance Scenarios**:

1. **Given** a valid, unexpired, unused OTP and a valid new password, **When** the reset-password request is submitted, **Then** the password is updated, the OTP is consumed, all prior sessions are invalidated, and a new JWT is returned so the user is immediately logged in.
2. **Given** an incorrect OTP, **When** the reset-password request is submitted, **Then** the request is rejected with an "invalid OTP" error code and the password is not changed.
3. **Given** an expired OTP, **When** the reset-password request is submitted, **Then** the request is rejected with an "OTP expired" error code distinct from an invalid OTP error.
4. **Given** an OTP that has already been used, **When** it is submitted again, **Then** the request is rejected with an "OTP already used" error code.
5. **Given** too many failed reset attempts for an email, **When** another attempt is made, **Then** the OTP is invalidated and the request is rejected with a "max attempts exceeded" error code, directing the user to request a new OTP.
6. **Given** a valid OTP but an invalid new password (e.g., too short), **When** the reset-password request is submitted, **Then** the request is rejected with a validation error and the OTP remains usable.
7. **Given** a successful reset, **When** the user tries to use any previously issued authentication token, **Then** the token is rejected and they must log in again.

---

### User Story 4 - Logout All Devices (Priority: P4)

A logged-in user, suspecting their account has been accessed without authorisation, wants to revoke all active sessions across every device. After triggering this action, only a fresh login will restore access.

**Why this priority**: A security hardening feature that requires the session/token infrastructure already in place. Important for user trust but depends on the authentication system being stable.

**Independent Test**: Can be fully tested by authenticating on two simulated devices (two tokens), calling the logout-all-devices endpoint with one token, then verifying that both tokens are subsequently rejected.

**Acceptance Scenarios**:

1. **Given** a logged-in user with multiple active tokens, **When** they call logout-all-devices, **Then** all tokens for that user are invalidated including the one used to make the request.
2. **Given** an unauthenticated request, **When** the logout-all-devices endpoint is called, **Then** a 401 Unauthorized response is returned.
3. **Given** a successful logout-all-devices, **When** any previously issued token for that user is used, **Then** the token is rejected.

### Edge Cases

- What happens when the OTP email delivery fails? The system should return an error so the user knows to retry or use resend-OTP.
- What happens when a password is changed while a reset OTP is pending? The pending OTP should be invalidated.
- What happens when the user account is locked or inactive and they attempt forgot-password? The system should return a generic response (no enumeration).
- What happens when change-password and logout-all-devices are called concurrently? The system must handle this atomically without leaving partial state.
- What happens when a valid OTP is submitted with an invalid new password? The OTP should not be consumed so the user can correct their password and resubmit.

## Requirements *(mandatory)*

### Functional Requirements

**Change Password**

- **FR-001**: System MUST allow authenticated users to change their password by verifying their current password before accepting the new one.
- **FR-002**: System MUST reject a new password that is identical to the current password during change-password.
- **FR-003**: System MUST enforce password complexity rules consistently across change-password and reset-password flows.
- **FR-003a**: System MUST invalidate all sessions for the user except the current one upon a successful change-password.
- **FR-003b**: System MUST invalidate any pending (unused, unexpired) reset OTP for the user when their password is changed, preventing the OTP from being used after the password has already been updated.

**Forgot Password & Resend OTP**

- **FR-004**: System MUST provide a forgot-password endpoint that accepts an email address and initiates OTP delivery without revealing whether the email is registered.
- **FR-005**: System MUST generate a cryptographically random, time-limited OTP (default: 10 minutes) when a forgot-password request is made.
- **FR-006**: System MUST provide a resend-OTP endpoint that invalidates any existing pending OTP for the email and sends a fresh one.
- **FR-007**: System MUST apply rate limiting to both the forgot-password and resend-OTP endpoints to prevent abuse.

**Reset Password**

- **FR-008**: System MUST provide a reset-password endpoint that accepts an email, OTP, and new password, and updates the password only when the OTP is valid and unexpired.
- **FR-008a**: System MUST return distinct error codes for each OTP failure mode: invalid OTP, OTP expired, OTP already used, and max attempts exceeded — so the client can display the appropriate guidance to the user.
- **FR-009**: System MUST consume (mark as used) the OTP after a successful password reset so it cannot be replayed.
- **FR-010**: System MUST invalidate an OTP after 5 consecutive failed reset attempts, requiring the user to request a new one.
- **FR-011**: System MUST NOT consume the OTP when the reset fails due to password validation errors, so the user can correct their input and resubmit.
- **FR-012**: System MUST invalidate all existing authentication tokens for a user upon a successful password reset.
- **FR-012a**: System MUST issue a new JWT to the user upon a successful password reset, logging them in immediately without requiring a separate login step.

**Logout All Devices**

- **FR-013**: System MUST allow authenticated users to invalidate all their active sessions via a logout-all-devices action.

**Audit**

- **FR-014**: System MUST log all security-sensitive events (password change, OTP requests, password reset, session revocation) for audit purposes.

**OTP Cleanup**

- **FR-015**: System MUST delete OTP records 24 hours after their creation timestamp, regardless of whether they were used, expired, or invalidated. The audit log (FR-014) is the durable record; OTP rows are transient.

### Key Entities

- **OTP Record**: Represents a one-time password issued for a password reset. Attributes: associated email, hashed OTP value, creation timestamp, expiry timestamp, used flag, failed attempt count. Records are deleted 24 hours after creation regardless of state.
- **User Session / Token**: Represents an active authentication credential for a user. Must support bulk revocation per user (e.g., via a security stamp or token version counter).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete the change-password flow in under 1 minute when they know their current password.
- **SC-002**: Users receive a forgot-password OTP within 60 seconds of submitting their email (subject to email delivery).
- **SC-003**: Users can complete the full forgot-password → resend if needed → reset-password flow in under 5 minutes.
- **SC-004**: All affected sessions are invalidated within 1 second of a successful password change (other sessions), password reset (all sessions), or logout-all-devices (all sessions).
- **SC-005**: OTP brute-force is prevented — an OTP is invalidated after 5 consecutive failed reset attempts, requiring the user to request a new one.
- **SC-006**: No user enumeration is possible via the forgot-password or resend-OTP endpoints — response time and content are identical for registered and unregistered emails.

## Assumptions

- Users are already registered and have a verified email address on file; email verification is out of scope for this feature.
- OTPs are 6-digit numeric codes delivered by email; the email delivery mechanism (SMTP or third-party service) is already configured or will be provided by the infrastructure layer.
- The reset-password endpoint accepts email, OTP, and new password in a single request (Pattern A) — OTP verification and password change happen atomically with no intermediate token.
- Token revocation is implemented via a security stamp / token version mechanism on the user record rather than a token denylist, consistent with the existing JWT setup.
- Password complexity rules are already defined in the Identity configuration; this feature reuses them without introducing new rules.
- The logout-all-devices action does not require the user to re-enter their password (they are already authenticated).
- Rate limiting configuration (thresholds, windows) may be adjusted during implementation without revisiting this spec.
