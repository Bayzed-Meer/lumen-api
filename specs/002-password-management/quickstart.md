# Quickstart: Password Management & Session Control

## Prerequisites

- PostgreSQL running and `ASPNETCORE_ENVIRONMENT=Development` set (handled by `launchSettings.json`)
- User secrets configured: `dotnet user-secrets list` should show DB connection string + JWT settings + SMTP settings
- A verified user account (email + password known)

## Run the API

```bash
dotnet run --project src/Lumen.API
# API available at http://localhost:5148
# Swagger UI at http://localhost:5148/swagger
```

## Apply migrations (after this feature is implemented)

```bash
dotnet ef migrations add AddOtpPurposeAndIsUsed \
  --project src/Lumen.Infrastructure \
  --startup-project src/Lumen.API

dotnet ef database update \
  --project src/Lumen.Infrastructure \
  --startup-project src/Lumen.API
```

---

## Manual Test Flows

### Story 1 — Change Password

```bash
# 1. Log in
curl -c cookies.txt -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"user@example.com","password":"OldPass1!"}'
# → save the accessToken from response

# 2. Change password
curl -b cookies.txt -X POST http://localhost:5148/api/auth/change-password \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <accessToken>" \
  -d '{"currentPassword":"OldPass1!","newPassword":"NewPass2@"}'
# → 200 with new accessToken; new refreshToken cookie set

# 3. Verify old password is rejected
curl -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"user@example.com","password":"OldPass1!"}'
# → 401

# 4. Verify new password works
curl -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"user@example.com","password":"NewPass2@"}'
# → 200
```

### Story 2 & 3 — Forgot Password / Reset Password

```bash
# 1. Initiate forgot-password (always 200 regardless of email)
curl -X POST http://localhost:5148/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'
# → 200 with generic message
# → Check SMTP inbox / MailKit dev sink for OTP code

# 2. Reset password with OTP
curl -c reset_cookies.txt -X POST http://localhost:5148/api/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","otp":"<6-digit-code>","newPassword":"Reset1Pass!"}'
# → 200 with new accessToken; new refreshToken cookie set (logged in immediately)

# 3. Test OTP already used
curl -X POST http://localhost:5148/api/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","otp":"<same-code>","newPassword":"Reset1Pass!"}'
# → 400 with errorCode: "otp_already_used"

# 4. Resend OTP (invalidates previous, sends new)
curl -X POST http://localhost:5148/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'
curl -X POST http://localhost:5148/api/auth/resend-reset-otp \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'
# → new code in inbox; old code rejected as "invalid_otp" or "otp_already_used"
```

### Story 4 — Logout All Devices

```bash
# 1. Log in on "device A"
curl -c device_a.txt -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"user@example.com","password":"Reset1Pass!"}'
# → save accessToken as <tokenA>

# 2. Log in on "device B" (separate cookie jar)
curl -c device_b.txt -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"user@example.com","password":"Reset1Pass!"}'

# 3. Logout all devices using device A's token
curl -b device_a.txt -X POST http://localhost:5148/api/auth/logout-all-devices \
  -H "Authorization: Bearer <tokenA>"
# → 204

# 4. Verify device B's refresh token is revoked
curl -b device_b.txt -X POST http://localhost:5148/api/auth/refresh
# → 401
```

---

## Unit Tests

```bash
dotnet test --filter "FullyQualifiedName~PasswordServiceTests"
dotnet test   # full suite — must be zero failures
```

---

## Rate Limit Test

```bash
# Send 4 requests in quick succession to forgot endpoint
for i in {1..4}; do
  curl -o /dev/null -s -w "%{http_code}\n" \
    -X POST http://localhost:5148/api/auth/forgot-password \
    -H "Content-Type: application/json" \
    -d '{"email":"anyone@example.com"}'
done
# → 200 200 200 429
```
