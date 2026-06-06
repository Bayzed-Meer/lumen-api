# API Contracts: Password Management

**Base path**: `/api/auth`  
**Auth**: Endpoints marked `[Authorize]` require a valid Bearer JWT in the `Authorization` header.

---

## POST /api/auth/change-password

**Auth**: Required (`[Authorize]`)  
**Rate limit**: None (protected by authentication)

### Request

```json
{
  "currentPassword": "string",   // required
  "newPassword":     "string"    // required; must satisfy Identity password rules
}
```

### Responses

**200 OK** — Password changed. Returns new tokens; new refresh token also set as `refreshToken` HttpOnly cookie (`Path=/api/auth`).

```json
{
  "accessToken":      "string",
  "expiresInSeconds": 900
}
```

**400 Bad Request** — `currentPassword` is wrong, `newPassword` equals `currentPassword`, or `newPassword` fails complexity rules. RFC 7807.

```json
{
  "type":   "https://tools.ietf.org/html/rfc7807",
  "title":  "Bad Request",
  "status": 400,
  "errors": {
    "newPassword": ["..."]
  }
}
```

**401 Unauthorized** — Missing or invalid JWT.

---

## POST /api/auth/forgot-password

**Auth**: None  
**Rate limit**: Fixed window — 3 requests per minute per IP (`password-reset` policy)

### Request

```json
{
  "email": "string"   // required; valid email format
}
```

### Responses

**200 OK** — Always returned regardless of whether the email is registered (user enumeration prevention).

```json
{
  "message": "A reset code has been sent."
}
```

**429 Too Many Requests** — Rate limit exceeded.

---

## POST /api/auth/resend-reset-otp

**Auth**: None  
**Rate limit**: Fixed window — 3 requests per minute per IP (`password-reset` policy)

### Request

```json
{
  "email": "string"   // required; valid email format
}
```

### Responses

**200 OK** — Always returned regardless of whether the email is registered.

```json
{
  "message": "A reset code has been sent."
}
```

**429 Too Many Requests** — Rate limit exceeded.

---

## POST /api/auth/reset-password

**Auth**: None  
**Rate limit**: None (OTP brute-force protection is built into the domain)

### Request

```json
{
  "email":       "string",   // required; valid email format
  "otp":         "string",   // required; 6-digit numeric
  "newPassword": "string"    // required; must satisfy Identity password rules
}
```

### Responses

**200 OK** — OTP verified and password reset. Returns new tokens; new refresh token set as HttpOnly cookie.

```json
{
  "accessToken":      "string",
  "expiresInSeconds": 900
}
```

**400 Bad Request** — OTP or password failure. Error detail includes a machine-readable `errorCode` extension field so clients can display appropriate guidance.

```json
{
  "type":      "https://tools.ietf.org/html/rfc7807",
  "title":     "Bad Request",
  "status":    400,
  "errorCode": "invalid_otp",          // see table below
  "detail":    "Human-readable message."
}
```

| `errorCode` | Meaning | OTP consumed? |
|---|---|---|
| `invalid_otp` | Wrong code | No — remaining attempts decremented |
| `otp_expired` | Past expiry | No |
| `otp_already_used` | Already consumed | No |
| `max_attempts_exceeded` | 5 consecutive failures | OTP was locked on last attempt |
| `no_otp_requested` | No reset was ever requested | No |
| *(none — password validation failure)* | `newPassword` fails complexity | No |

**401 Unauthorized** — Email not found (returned as 401 to avoid confirming email existence; only the OTP proves ownership).

---

## POST /api/auth/logout-all-devices

**Auth**: Required (`[Authorize]`)  
**Rate limit**: None

### Request

No body.

### Responses

**204 No Content** — All sessions revoked. `refreshToken` cookie deleted.

**401 Unauthorized** — Missing or invalid JWT.

---

## Notes

- All error responses conform to RFC 7807 Problem Details. Controllers use `Problem()` / `ValidationProblem()`.
- The `refreshToken` cookie is `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`.
- Short-lived access tokens (default 15 minutes) expire naturally after session revocation; no access-token denylist is maintained.
