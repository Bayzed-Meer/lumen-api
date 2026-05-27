# API Contract: Auth — `/api/auth`

**Branch**: `001-user-auth` | **Date**: 2026-05-23

All responses follow RFC 7807 Problem Details for errors.

**Refresh token transport**: The refresh token is delivered and received exclusively via an `HttpOnly; Secure; SameSite=Strict` cookie named `refreshToken`. It is never present in the JSON response body. Clients must send cookies (e.g. `credentials: 'include'` in fetch, or cookie jar in REST clients) on requests to `/api/auth/refresh` and `/api/auth/logout`.

---

## POST /api/auth/login — Login

Authenticates a verified user using either their email or institutional ID plus password. Returns a JWT access token in the body and sets the refresh token as an HttpOnly cookie.

**Authorization**: `[AllowAnonymous]`

### Request

```http
POST /api/auth/login
Content-Type: application/json
```

```json
{
  "identity": "john.doe@university.edu",
  "password": "Secure!Pass1"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `identity` | `string` | Yes | Email address OR any institutional ID (student/faculty/librarian); the system searches all namespaces automatically |
| `password` | `string` | Yes | |

### Responses

#### 200 OK

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresIn": 900
}
```

Response also includes:

```
Set-Cookie: refreshToken=<opaque-value>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=604800
```

| Field | Type | Notes |
|-------|------|-------|
| `accessToken` | `string` | JWT; lifetime 900 seconds (15 min) |
| `accessTokenExpiresIn` | `int` | Seconds until access token expiry |

The refresh token value is only available in the cookie, never in the JSON body.

#### 401 Unauthorized — All Failure Modes

A single generic message is returned for all failure reasons (wrong password, identity not found, or unverified account). This prevents information disclosure about whether an identity exists or an account's verification state.

```json
{
  "status": 401,
  "detail": "Invalid credentials."
}
```

---

## POST /api/auth/refresh — Refresh Token

Exchanges the refresh token cookie for a new access token + refresh token pair. The submitted cookie is immediately invalidated and replaced (rotation on every use, FR-021).

**Authorization**: `[AllowAnonymous]`

**Cookie required**: `refreshToken` HttpOnly cookie must be present.

### Request

```http
POST /api/auth/refresh
Cookie: refreshToken=<opaque-value>
```

No request body.

### Responses

#### 200 OK

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresIn": 900
}
```

Response also replaces the cookie:

```
Set-Cookie: refreshToken=<new-opaque-value>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=604800
```

#### 401 Unauthorized — Invalid, Expired, or Revoked Token

```json
{
  "status": 401,
  "detail": "Invalid credentials."
}
```

**Token reuse detection**: if an already-rotated (revoked) cookie value is submitted, the entire token family for that user is revoked and this 401 is returned. The user must log in again.

---

## POST /api/auth/logout — Logout

Revokes the current refresh token cookie immediately. The access token expires naturally within its remaining lifetime (≤ 15 min) — no new access tokens can be issued once the refresh token is gone. Other active sessions on other devices remain unaffected.

**Authorization**: `[Authorize]`

**Cookie required**: `refreshToken` HttpOnly cookie must be present.

### Request

```http
POST /api/auth/logout
Authorization: Bearer <access-token>
Cookie: refreshToken=<opaque-value>
```

No request body.

### Responses

#### 204 No Content

Logout successful. The refresh token cookie is revoked and cleared. The access token will expire naturally within its remaining lifetime (≤ 15 min).

Response also clears the cookie:

```
Set-Cookie: refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Expires=Thu, 01 Jan 1970 00:00:00 GMT
```

#### 401 Unauthorized

Access token missing or expired.
