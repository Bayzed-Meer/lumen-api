# Developer Quickstart: User Auth Feature

**Branch**: `001-user-auth` | **Date**: 2026-05-23

## Prerequisites

- .NET 10 SDK
- PostgreSQL running locally (or Docker)
- A working SMTP server or Mailpit/MailHog for local email capture

## 1. Database Setup

Ensure PostgreSQL is running and create a database:

```sql
CREATE DATABASE lumen_dev;
```

Set the connection string via user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=lumen_dev;Username=postgres;Password=yourpassword" \
  --project src/Lumen.API
```

## 2. JWT Configuration

Set the JWT signing key (min 32 characters for HMAC-SHA256):

```bash
dotnet user-secrets set "Jwt:Key" "your-super-secret-key-at-least-32-chars" \
  --project src/Lumen.API
```

The issuer, audience, and token lifetimes are in `appsettings.json` (no secrets needed):

```json
{
  "Jwt": {
    "Issuer": "lumen-api",
    "Audience": "lumen-client",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

## 3. SMTP / Email Configuration

For local development, use [Mailpit](https://github.com/axllent/mailpit) (runs in Docker, captures all outgoing mail):

```bash
docker run -d -p 1025:1025 -p 8025:8025 axllent/mailpit
```

Set SMTP credentials:

```bash
dotnet user-secrets set "Smtp:Host" "localhost" --project src/Lumen.API
dotnet user-secrets set "Smtp:Port" "1025" --project src/Lumen.API
dotnet user-secrets set "Smtp:Username" "" --project src/Lumen.API
dotnet user-secrets set "Smtp:Password" "" --project src/Lumen.API
dotnet user-secrets set "Smtp:FromAddress" "noreply@lumen.library" --project src/Lumen.API
dotnet user-secrets set "Smtp:FromName" "Lumen Library" --project src/Lumen.API
```

Mailpit web UI is at `http://localhost:8025` — all outgoing OTP emails appear there.

## 4. Admin Account Seeding

Set the admin seed credentials:

```bash
dotnet user-secrets set "AdminSeed:Email" "admin@lumen.library" --project src/Lumen.API
dotnet user-secrets set "AdminSeed:Password" "Admin!Seed1" --project src/Lumen.API
dotnet user-secrets set "AdminSeed:FirstName" "System" --project src/Lumen.API
dotnet user-secrets set "AdminSeed:LastName" "Admin" --project src/Lumen.API
```

## 5. Apply Migrations and Run

```bash
dotnet ef database update \
  --project src/Lumen.Infrastructure \
  --startup-project src/Lumen.API

dotnet run --project src/Lumen.API
```

The API starts at `http://localhost:5148`. Swagger UI is at `http://localhost:5148/swagger`.

## 6. Manual Testing Walkthrough

### Step 1 — Login as admin

```bash
curl -s -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"admin@lumen.library","password":"Admin!Seed1"}' | jq .
```

Copy the `accessToken` value.

### Step 2 — Create a student account

```bash
curl -s -X POST http://localhost:5148/api/register \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <access-token>" \
  -d '{
    "institutionalId": "STU-2024-001",
    "email": "alice@student.edu",
    "firstName": "Alice",
    "lastName": "Smith",
    "password": "Student!Pass1",
    "role": "Student"
  }' | jq .
```

Expected: `201` with `"isVerified": false`.

### Step 3 — Check OTP email

Open Mailpit at `http://localhost:8025` and find the OTP email sent to `alice@student.edu`.

### Step 4 — Verify OTP

```bash
curl -s -X POST http://localhost:5148/api/register/verify \
  -H "Content-Type: application/json" \
  -d '{"identity":"alice@student.edu","otp":"<otp-from-email>"}' | jq .
```

Expected: `200 OK`.

### Step 5 — Login as student

```bash
curl -s -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"STU-2024-001","password":"Student!Pass1"}' | jq .
```

Expected: `200` with `accessToken` and `refreshToken`.

### Step 6 — Logout

The refresh token lives in an HttpOnly cookie. Use `-c` (cookie jar) and `-b` (send cookies) with curl:

```bash
# Login and save cookies to a jar file
curl -s -c /tmp/lumen-cookies.txt \
  -X POST http://localhost:5148/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identity":"alice@student.edu","password":"Student!Pass1"}' | jq .

# Logout — cookie jar sends the refreshToken cookie automatically
curl -s -c /tmp/lumen-cookies.txt -b /tmp/lumen-cookies.txt \
  -X POST http://localhost:5148/api/auth/logout \
  -H "Authorization: Bearer <access-token>" \
  -w "%{http_code}"
```

Expected: `204 No Content`.

### Step 7 — Confirm tokens are invalid

Attempt to use the same `accessToken` on a protected endpoint — expect `401`. Attempt to call `/api/auth/refresh` with the cleared cookie jar — expect `401` (cookie was cleared by logout).

## 7. Running Tests

```bash
dotnet test
```

All tests must pass. Coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```
