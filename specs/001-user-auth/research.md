# Research: User Authentication & Role-Based Account Management

**Branch**: `001-user-auth` | **Date**: 2026-05-23

## 1. JWT Authentication in ASP.NET Core

**Decision**: `Microsoft.AspNetCore.Authentication.JwtBearer` with HMAC-SHA256 signing.

**Rationale**: Standard, zero external IdP required, natively supported by ASP.NET Core. HMAC-SHA256 (symmetric) is appropriate here — the API both signs and verifies its own tokens, so asymmetric RSA adds unnecessary key management complexity.

**Access token claims**:
- `sub` — ApplicationUser.Id (the IdentityUser string GUID)
- `email` — user's email
- `role` — single role value (Admin/Librarian/Student/Faculty)
- `jti` — unique token ID (GUID); standard claim, included by default
- `exp`, `iat`, `iss`, `aud` — standard JWT claims

**Configuration keys** (never hardcoded):
```json
{
  "Jwt": {
    "Issuer": "lumen-api",
    "Audience": "lumen-client",
    "Key": "",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```
`Key` is left empty in `appsettings.json`; set via user-secrets (dev) or environment variable `Jwt__Key` (prod).

**Alternatives considered**:
- RSA asymmetric signing — rejected: unnecessary complexity for a single-service API
- External IdP (Auth0, Azure AD) — rejected by constitution III

---

## 2. Refresh Token Strategy

**Decision**: DB-stored refresh tokens with family-based reuse detection; delivered via `HttpOnly; Secure; SameSite=Strict` cookie — never in the response body.

**Rationale**: An `HttpOnly` cookie prevents JavaScript access, eliminating XSS-based token theft. `Secure` enforces HTTPS-only transport. `SameSite=Strict` prevents the cookie from being sent in cross-site requests. Combined with DB-side SHA-256 hashing, this protects against both client-side and server-side token theft. Token family tracking detects reuse of a stolen (rotated) token and revokes the entire family, forcing re-login.

**Cookie attributes**:
```
Set-Cookie: refreshToken=<value>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=604800
```
- `Path=/api/auth` — cookie is only sent to `/api/auth/*` endpoints, not every request
- `Max-Age=604800` — 7 days in seconds (matches token lifetime)
- In development (HTTP only), `Secure` is conditionally omitted via `CookieSecurePolicy.SameAsRequest`

**Flow**:
1. On login: create `RefreshToken` record → set cookie in response; no `refreshToken` field in JSON body
2. On refresh (`POST /api/auth/refresh`): read token from cookie → verify hash + `!IsRevoked` + `ExpiresAt > now` → revoke old token → issue new pair → set new cookie
3. On logout (`POST /api/auth/logout`): read cookie → revoke `RefreshToken` record → clear cookie (expired `Set-Cookie` header)
4. On reuse of revoked token: revoke entire family chain for that user → return 401 → user must log in again

**Family chain query**: walk `ReplacedByTokenId` ancestors to find the root; mark all descendant tokens `IsRevoked = true`.

**Alternatives considered**:
- Refresh token in response body — rejected: accessible to JavaScript (XSS theft risk)
- Redis TTL-based storage — rejected: external infrastructure dependency
- Opaque tokens stored plain — rejected: DB dump would expose live tokens

---

## 3. OTP Generation & Storage

**Decision**: `RandomNumberGenerator.GetInt32(100000, 1000000)` → 6-digit code; stored as SHA-256 hash in DB.

**Rationale**: `System.Security.Cryptography.RandomNumberGenerator` provides cryptographic randomness without bias. Hashing prevents plaintext code exposure from a DB dump while remaining simple to verify (hash the submitted code and compare).

**OTP lifecycle**:
1. Generate → hash → store `OtpRecord` with `IssuedAt`, `ExpiresAt = IssuedAt + 10 min`, `FailedAttempts = 0`, `IsInvalidated = false`
2. Verify → hash submitted code → compare → increment `FailedAttempts` on mismatch
3. After 5 failures → reject all further attempts; user must call resend
4. Resend → mark current active OTP `IsInvalidated = true`, reset `FailedAttempts`, issue new OTP

**Only one active OTP per user** (FR-015): query filters by `!IsInvalidated && ExpiresAt > now`.

**Alternatives considered**:
- TOTP (RFC 6238) — rejected: requires shared secret setup; OTP is creator-initiated, not user-initiated
- Storing plaintext OTP — rejected: DB dump risk

---

## 4. Email Service

**Decision**: `IEmailService` abstraction in Application; `SmtpEmailService` in Infrastructure using `MailKit`.

**Rationale**: MailKit is the recommended replacement for the deprecated `System.Net.Mail.SmtpClient`. The abstraction keeps Application layer independent of the SMTP implementation and makes unit testing trivial (mock `IEmailService`).

**Configuration**:
```json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "",
    "Password": "",
    "FromAddress": "noreply@lumen.library",
    "FromName": "Lumen Library"
  }
}
```
`Password` via user-secrets / `Smtp__Password` env var.

**OTP email failure handling** (FR-010): `SmtpEmailService` logs the failure with `LogWarning`. The account is still created unverified. The caller does not throw; account creation returns 201 regardless of email delivery success.

**Alternatives considered**:
- `System.Net.Mail.SmtpClient` — rejected: deprecated in .NET
- SendGrid / Mailgun SDK — rejected: external service dependency out of scope; MailKit + SMTP is sufficient

---

## 5. Login with Institutional ID

**Decision**: Sequential query across profile tables in `IdentityService`; first match wins; no role declaration required.

**Rationale**: Simple and correct. The three profile tables (Student, Faculty, Librarian) have independent ID namespaces, so a single value can appear in at most one table. Searching all three takes at most 3 DB queries (typically 1–2).

**Flow in `IdentityService.FindUserByInstitutionalIdAsync`**:
1. Query `Students` where `StudentId == identity` → return `UserId` if found
2. Query `Faculty` where `InstitutionalId == identity` → return `UserId` if found
3. Query `Librarians` where `LibrarianId == identity` → return `UserId` if found
4. Return `null` if not found in any namespace

**Alternatives considered**:
- Single unified ID table — rejected: complicates uniqueness constraints and per-role isolation
- Union SQL query — rejected: EF Core LINQ is sufficient; raw SQL adds complexity

---

## 6. Role Permission Model

**Decision**: Coarse-grained via ASP.NET Core roles (`[Authorize(Roles = "Admin,Librarian")]`); fine-grained in Application layer service.

**Coarse gate**: `RegisterController` requires `Admin` or `Librarian` role for the create-account endpoint (students/faculty return 403 automatically from the framework).

**Fine gate in `RegisterService.CreateAccountAsync`**: check the calling user's role against the target role:
- If caller is `Librarian` and target role is `Librarian` → throw `ForbiddenException`
- All other caller/target combinations that reach this point are already permitted

**Alternatives considered**:
- Policy-based authorization — rejected: overkill for two simple rules that map cleanly to role checks
- Resource-based authorization — rejected: no resource ownership model needed here

---

## 7. Access Token Behaviour After Logout

**Decision**: Accept the 15-minute natural expiry window. No blacklist table. Logout only revokes the refresh token.

**Rationale**: A blacklist table adds a DB query to every authenticated request — on every endpoint, for every user. The benefit is immediate access token invalidation on logout. For a library system this cost is disproportionate. The access token is short-lived (15 min); after logout the refresh token is gone, so no new access tokens can be issued. The worst case is a logged-out token remains usable for up to 15 minutes — an accepted tradeoff in the majority of production systems.

**Logout flow**:
1. Revoke the `RefreshToken` record in the DB → immediately prevents new access tokens from being issued
2. Clear the `refreshToken` HttpOnly cookie → client can no longer send it
3. The existing access token expires naturally within its remaining lifetime (≤ 15 min)

**Alternatives considered**:
- `RevokedAccessToken` DB table (JTI blacklist) — rejected: extra DB query on every protected request; complexity not justified for this system's scale
- Redis blacklist — rejected: external infrastructure dependency

---

## 8. Concurrent Registration (Same Email)

**Decision**: Rely on the PostgreSQL unique constraint on `AspNetUsers.NormalizedEmail` (enforced by Identity) and a unique index on each profile table's institutional ID.

**Rationale**: The DB unique constraint is the authoritative guard. `UserManager.CreateAsync` returns an `IdentityResult` with a duplicate-email error if concurrent inserts violate it. The service converts this to a `ConflictException`. For institutional IDs, a unique index on each profile table catches duplicates at the DB level (`DbUpdateException` → mapped to `ConflictException`).

**Alternatives considered**:
- Application-level uniqueness check before insert — rejected: TOCTOU race condition; DB constraint is the only safe guard
