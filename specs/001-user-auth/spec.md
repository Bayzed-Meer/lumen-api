# Feature Specification: User Authentication & Role-Based Account Management

**Feature Branch**: `001-user-auth`

**Created**: 2026-05-23

**Status**: Draft

**Input**: User description: "I want to implement registration, login, logout feature. 1. Admin can create both librarian, student, faculty, librarian can create student, faculty but can't create librarian, student and faculty can't create any account. 3. For a account to be created each one has their unique id, like student has studentid, faculty has it's id, librarian has his id. So It must be there with unique email, firstname, lastname, password. 4. After submitting the details and 6 digit otp will be sent to the email, without the otp account will be created but no verified and can't be loggin until verify. 5. there should be a way to resend otp to make the account verifyable if something goes wrong. 6. for login user can use email or id + password"

## Clarifications

### Session 2026-05-23

- Q: Which token strategy should the login endpoint issue? → A: Access token (short-lived, ~15 min) + Refresh token (longer-lived, ~7 days); logout revokes the refresh token immediately; the access token expires naturally within its remaining lifetime.
- Q: Should the system restrict repeated failed OTP verification attempts? → A: Lock OTP verification after 5 failed attempts; the user must request a resend (which resets the counter) to try again.
- Q: How are institutional IDs assigned? → A: Manually entered by the creator at registration (pre-existing institutional ID); the system does not generate them.
- Q: If the OTP email fails to send, should account creation be rolled back? → A: No — create the account in unverified state regardless; the creator or user uses the resend endpoint to recover.
- Q: What password complexity should the system enforce? → A: Minimum 8 characters with at least one uppercase letter, one lowercase letter, one digit, and one special character.
- Q: When logging in with a institutional ID, must the user declare their role or does the system search all namespaces? → A: System searches all ID namespaces automatically; first match wins. No role field required at login.
- Q: Should the refresh token be rotated on every use? → A: Yes — rotate on every use; the old refresh token is invalidated and a new one issued alongside each new access token.
- Q: Should logout invalidate the current session only or all active sessions? → A: Current session only; other devices remain logged in.
- Q: Should the system limit consecutive failed login attempts? → A: No — unlimited login attempts permitted; login brute-force protection is out of scope for this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Creates Any Account (Priority: P1)

An administrator needs to on-board new library members. The admin submits the required details — institutional ID, email, first name, last name, and password — for a new librarian, student, or faculty member. The system registers the account in an unverified state and immediately sends a 6-digit OTP to the new user's email address. The new user must enter that OTP at the verification endpoint before they can log in.

**Why this priority**: Core bootstrapping capability — without it, no other users can be created. All other account-creation flows depend on librarian accounts existing first.

**Independent Test**: An admin account is seeded at startup (already exists). Call "create librarian account" with valid details, confirm a 201 response, confirm the account exists but is unverified, and confirm an OTP was dispatched.

**Acceptance Scenarios**:

1. **Given** a logged-in admin, **When** they submit valid details (librarian ID, email, first name, last name, password) to create a librarian account, **Then** the account is created in an unverified state and an OTP is sent to the provided email.
2. **Given** a logged-in admin, **When** they submit a duplicate email or a duplicate institutional ID, **Then** the system rejects the request with a clear conflict error.
3. **Given** a logged-in admin, **When** they omit any required field, **Then** the system rejects the request with a validation error listing the missing fields.

---

### User Story 2 - Librarian Creates Student or Faculty Account (Priority: P2)

A librarian needs to register new students or faculty members. They submit the same required fields. The system applies the same OTP-verification flow as admin-created accounts. Librarians are explicitly blocked from creating other librarian accounts.

**Why this priority**: Librarians are the day-to-day administrators; they must be able to on-board users without needing the admin.

**Independent Test**: Log in as a librarian, create a student account with valid details, confirm success. Attempt to create a librarian account, confirm a 403 response.

**Acceptance Scenarios**:

1. **Given** a logged-in librarian, **When** they submit valid details for a student account, **Then** the account is created unverified and an OTP is sent to the student's email.
2. **Given** a logged-in librarian, **When** they submit valid details for a faculty account, **Then** the account is created unverified and an OTP is sent to the faculty member's email.
3. **Given** a logged-in librarian, **When** they attempt to create a librarian account, **Then** the system rejects the request with a 403 Forbidden response.

---

### User Story 3 - New User Verifies Account via OTP (Priority: P3)

After their account is created, a new user checks their email for the 6-digit OTP. They submit it along with their identity (email or institutional ID) to the verification endpoint. On success, their account becomes verified and they may log in.

**Why this priority**: Without verification, no newly created user can log in — this is the gate between account creation and usability.

**Independent Test**: Create a student account, retrieve the OTP (from the mail service or a test hook), submit it to the verify endpoint, confirm the account is now verified.

**Acceptance Scenarios**:

1. **Given** an unverified account, **When** the user submits the correct OTP, **Then** the account transitions to verified status and a success response is returned.
2. **Given** an unverified account, **When** the user submits an incorrect OTP, **Then** the system rejects the request with an error and the account remains unverified.
3. **Given** an unverified account with an expired OTP, **When** the user attempts to verify, **Then** the system rejects the OTP and instructs the user to request a new one.

---

### User Story 4 - Resend OTP (Priority: P4)

A new user did not receive the OTP email (spam filter, typo in email, expired code). They request a new OTP via the resend endpoint. The system invalidates the previous OTP and sends a fresh 6-digit code to the account's email address.

**Why this priority**: Without resend, a failed OTP delivery permanently locks a user out, causing support escalation.

**Independent Test**: Create an account, call the resend endpoint, confirm a new OTP is dispatched and the old one is no longer valid.

**Acceptance Scenarios**:

1. **Given** an unverified account, **When** the user requests an OTP resend, **Then** a new 6-digit OTP is sent to their email and the previous OTP is invalidated.
2. **Given** an already-verified account, **When** the user requests an OTP resend, **Then** the system rejects the request indicating the account is already verified.

---

### User Story 5 - User Login (Priority: P5)

A verified user wants to access the system. They can authenticate using either their email + password or their institutional ID + password. On success they receive a token granting access to subsequent requests.

**Why this priority**: Login is the entry point for all system usage; it cannot be omitted. Placed P5 only because it depends on verified accounts existing.

**Independent Test**: Verify a test account, log in with email + password, confirm a token is returned. Log in with institutional ID + password, confirm the same.

**Acceptance Scenarios**:

1. **Given** a verified account, **When** the user submits correct email + password, **Then** the system returns a valid access token.
2. **Given** a verified account, **When** the user submits their institutional ID + password (without declaring their role), **Then** the system locates the account across all ID namespaces and returns a valid access token.
3. **Given** an unverified account, **When** the user attempts to log in, **Then** the system rejects the request and informs the user that email verification is required.
4. **Given** any account, **When** the user submits incorrect credentials, **Then** the system returns an authentication error without revealing which field was wrong.

---

### User Story 6 - User Logout (Priority: P6)

An authenticated user wants to end their session. They call the logout endpoint; the system immediately revokes their refresh token and clears the cookie so no new access tokens can be issued. The existing access token expires naturally within its remaining lifetime (≤ 15 min).

**Why this priority**: Required for security but functionally independent of the other stories.

**Independent Test**: Log in (receive access + refresh token), call logout, attempt to use each token separately, confirm both are rejected.

**Acceptance Scenarios**:

1. **Given** an authenticated user on one device, **When** they call the logout endpoint, **Then** the system revokes the refresh token for that session only, clears the refresh token cookie, and returns a success response. Any other active sessions on other devices remain valid. The access token expires naturally within its remaining lifetime (≤ 15 min).
2. **Given** a user who has logged out, **When** they attempt to use the same refresh token cookie to obtain a new access token, **Then** the system rejects it with an unauthorized error.
3. **Given** a user who has logged out, **When** their existing access token reaches its expiry time, **Then** the system rejects it with an unauthorized error and the user must log in again.

---

### User Story 7 - Students and Faculty Cannot Create Accounts (Priority: P2)

Students and faculty members must not be able to register other users regardless of what they submit.

**Why this priority**: Enforcing this constraint is as important as librarian account-creation; a privilege-escalation hole would undermine the whole access model.

**Independent Test**: Log in as a student, attempt to call any account-creation endpoint, confirm 403. Repeat as faculty.

**Acceptance Scenarios**:

1. **Given** a logged-in student, **When** they attempt to create any account, **Then** the system returns 403 Forbidden.
2. **Given** a logged-in faculty member, **When** they attempt to create any account, **Then** the system returns 403 Forbidden.

---

### Edge Cases

- What happens when two concurrent requests try to register the same email simultaneously? The system must reject one with a conflict error.
- What happens when the OTP is resent multiple times in quick succession? The system honours only the most recent OTP; each resend invalidates the previous code. A 1-minute cooldown between resend requests is enforced.
- What happens after 5 consecutive failed OTP attempts? The account's OTP verification is locked for 60 minutes. Both further verification attempts and resend requests are rejected during this window. After 60 minutes the lockout expires and the user may request a new code.
- What happens when a user attempts to log in with a non-existent email or ID? The system must return a generic "invalid credentials" message, not reveal whether the identity exists.
- What happens when an account is created with a institutional ID that already belongs to a user of a different role? Each role's ID space is independent, so there is no conflict.
- What happens if the OTP email fails to deliver at registration time? The account is still created in an unverified state; the creator or user must use the resend endpoint to trigger a new delivery attempt once the email service recovers.
- What happens when a creator submits a password that does not meet the complexity policy? The system rejects the account creation request with a clear validation error listing the unmet requirements.
- What happens if a refresh token is used after it has already been rotated (possible token theft)? The system detects the reuse of a superseded token and invalidates the entire session, forcing the user to log in again.

## Requirements *(mandatory)*

### Functional Requirements

**Account Creation — Permissions**

- **FR-001**: Admins MUST be able to create accounts for all roles: librarian, student, and faculty.
- **FR-002**: Librarians MUST be able to create accounts for students and faculty only; attempts to create librarian accounts MUST be rejected with a permission error.
- **FR-003**: Students MUST NOT be able to create accounts for any role.
- **FR-004**: Faculty members MUST NOT be able to create accounts for any role.

**Account Creation — Data**

- **FR-005**: Every account MUST be created with: a role-specific unique ID (supplied by the creator — a pre-existing institutional ID), a unique email address, a first name, a last name, and a password.
- **FR-005a**: Passwords MUST meet minimum complexity: at least 8 characters, containing at least one uppercase letter, one lowercase letter, one digit, and one special character. Accounts MUST NOT be created with a password that fails this policy.
- **FR-006**: Student accounts MUST carry a student ID that is unique across all student accounts.
- **FR-007**: Faculty accounts MUST carry a faculty ID that is unique across all faculty accounts.
- **FR-008**: Librarian accounts MUST carry a librarian ID that is unique across all librarian accounts.
- **FR-009**: The system MUST reject account creation if the email or the institutional ID already exists.

**OTP Verification**

- **FR-010**: Upon account creation, the system MUST attempt to send a 6-digit OTP to the new user's registered email address. If delivery fails, the account is still persisted in an unverified state; the creator or user MUST use the resend endpoint to trigger a new delivery attempt.
- **FR-011**: Newly created accounts MUST be in an unverified state; unverified accounts MUST be blocked from logging in.
- **FR-012**: Users MUST be able to verify their account by submitting the correct OTP along with their identity (email or institutional ID).
- **FR-013**: An OTP MUST expire after 10 minutes of issuance.
- **FR-014**: The system MUST provide a resend-OTP endpoint that issues a new code and invalidates any previously active OTP for that account. A minimum 1-minute cooldown between resend requests MUST be enforced. The resend endpoint MUST be rejected while an account is under OTP lockout (see FR-015a).
- **FR-015**: Only one OTP MUST be active per account at any given time.
- **FR-015a**: The system MUST lock OTP verification for an account after 5 consecutive failed attempts. The lockout lasts 60 minutes; during this window both further verification attempts and resend requests MUST be rejected. The lockout expires automatically after 60 minutes, after which the user may request a new code via the resend endpoint.

**Login**

- **FR-016**: Users MUST be able to authenticate using their email address and password.
- **FR-017**: Users MUST be able to authenticate using their institutional ID and password. The system MUST search all role ID namespaces (student, faculty, librarian) automatically; no role declaration is required in the login request.
- **FR-018**: Only verified accounts MUST be permitted to log in.
- **FR-019**: Failed login attempts MUST return a generic error that does not disclose whether the email/ID exists or which field is incorrect.

**Logout**

- **FR-020**: Authenticated users MUST be able to log out; both the active access token and the refresh token for the current session MUST be invalidated. Other active sessions on other devices MUST remain unaffected.
- **FR-021**: The system MUST provide a token-refresh endpoint that accepts a valid refresh token, invalidates it, and issues both a new access token and a new refresh token (rotation on every use). Reuse of an already-rotated refresh token MUST invalidate the entire session.

### Key Entities

- **User Account**: Shared identity record with email (globally unique), first name, last name, hashed password, role (admin/librarian/student/faculty), and verification status (verified/unverified).
- **Student Profile**: Extends user account with a student ID (supplied by creator, unique across all students; format determined by the institution).
- **Faculty Profile**: Extends user account with a faculty ID (supplied by creator, unique across all faculty; format determined by the institution).
- **Librarian Profile**: Extends user account with a librarian ID (supplied by creator, unique across all librarians; format determined by the institution).
- **OTP Record**: 6-digit code linked to a user account, with issuance timestamp, expiry timestamp, a used/invalidated flag, and a consecutive failed-attempt counter (resets to zero on resend).
- **Session / Token**: Proof of authenticated access, tied to a verified user account and revocable on logout.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new account can be created and an OTP dispatched in under 5 seconds end-to-end under normal load.
- **SC-002**: The OTP email is delivered to the new user's inbox within 60 seconds of account creation.
- **SC-003**: Login with valid credentials completes in under 2 seconds.
- **SC-004**: 100% of role-based permission checks are enforced — no unauthorized account creation succeeds.
- **SC-005**: Zero unverified accounts are able to obtain a valid session token.
- **SC-006**: A resent OTP invalidates the previous code 100% of the time, eliminating dual-OTP acceptance.
- **SC-007**: Generic credential error messages prevent identity enumeration (audited by security review).

## Assumptions

- The OTP is verified by the **newly created user** (not the account creator). The creator provides the new user's email during registration; the OTP goes to that email, and the new user completes verification independently.
- The account creator (admin or librarian) sets the initial password during registration. The new user receives this password through a separate, out-of-band channel (e.g., in-person or internal communication). Password change after first login is out of scope for this feature.
- OTP expiry is 10 minutes — a standard default for email-based one-time codes.
- Email delivery relies on an external email/SMTP service already available to the system; configuring that service is out of scope for this feature.
- This system exposes an API only; there is no browser UI. All interactions are via HTTP endpoints.
- Session management uses an access token (~15 min lifetime) and a refresh token (~7 days lifetime). Logout revokes the refresh token immediately in the DB and clears the HttpOnly cookie — the access token expires naturally within its remaining lifetime. The token-refresh endpoint rotates the refresh token on every use — the old one is invalidated and a new pair (access + refresh) is issued.
- Admin and faculty IDs live in separate namespaces, so a student ID of "S001" and a faculty ID of "F001" can coexist without conflict.
- The admin account is pre-seeded at startup (per existing project setup) and does not go through the OTP flow.
- Rate-limiting on the resend-OTP endpoint is desirable but is a separate concern outside this feature's scope.
