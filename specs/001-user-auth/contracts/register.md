# API Contract: Registration — `/api/register`

**Branch**: `001-user-auth` | **Date**: 2026-05-23

All responses follow RFC 7807 Problem Details for errors.

---

## POST /api/register — Create Account

Creates a new user account in an unverified state and dispatches a 6-digit OTP to the provided email.

**Authorization**: `[Authorize(Roles = "Admin,Librarian")]`

### Request

```http
POST /api/register
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{
  "institutionalId": "STU-2024-001",
  "email": "john.doe@university.edu",
  "firstName": "John",
  "lastName": "Doe",
  "password": "Secure!Pass1",
  "role": "Student"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `institutionalId` | `string` | Yes | Max 50 chars; unique within the target role's namespace |
| `email` | `string` | Yes | Valid email format; globally unique across all users |
| `firstName` | `string` | Yes | Max 100 chars |
| `lastName` | `string` | Yes | Max 100 chars |
| `password` | `string` | Yes | Min 8 chars; at least 1 uppercase, 1 lowercase, 1 digit, 1 special char |
| `role` | `string` | Yes | One of: `"Student"`, `"Faculty"`, `"Librarian"` (Admin cannot be created via this endpoint) |

**Note**: Librarians may only supply `"Student"` or `"Faculty"` for `role`. Supplying `"Librarian"` returns 403.

### Responses

#### 201 Created

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@university.edu",
  "role": "Student",
  "isVerified": false
}
```

#### 400 Bad Request — Validation Error

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation failed.",
  "status": 400,
  "errors": {
    "password": ["Password must be at least 8 characters."],
    "email": ["The Email field is required."]
  }
}
```

#### 401 Unauthorized

Token missing or expired.

#### 403 Forbidden

Caller is a Librarian attempting to create a Librarian account, or caller is Student/Faculty.

```json
{
  "status": 403,
  "detail": "Librarians may not create accounts for other librarians."
}
```

#### 409 Conflict

Email or institutional ID already exists.

```json
{
  "status": 409,
  "detail": "An account with this email already exists."
}
```

---

## POST /api/register/verify — Verify OTP

Marks an unverified account as verified after confirming the correct OTP.

**Authorization**: `[AllowAnonymous]`

### Request

```http
POST /api/register/verify
Content-Type: application/json
```

```json
{
  "identity": "john.doe@university.edu",
  "otp": "482931"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `identity` | `string` | Yes | Email address OR institutional ID |
| `otp` | `string` | Yes | Exactly 6 digits |

### Responses

#### 200 OK

```json
{
  "message": "Account verified successfully."
}
```

#### 400 Bad Request — Invalid OTP

```json
{
  "status": 400,
  "detail": "The OTP is incorrect."
}
```

#### 400 Bad Request — Expired OTP

```json
{
  "status": 400,
  "detail": "The OTP has expired. Please request a new one."
}
```

#### 400 Bad Request — OTP Locked (5 failed attempts)

```json
{
  "status": 400,
  "detail": "OTP verification is locked after 5 failed attempts. Please request a new OTP."
}
```

#### 404 Not Found

```json
{
  "status": 404,
  "detail": "Account not found."
}
```

#### 409 Conflict — Already Verified

```json
{
  "status": 409,
  "detail": "This account is already verified."
}
```

---

## POST /api/register/resend-otp — Resend OTP

Issues a new OTP, invalidating the previous one, and sends it to the account's registered email.

**Authorization**: `[AllowAnonymous]`

### Request

```http
POST /api/register/resend-otp
Content-Type: application/json
```

```json
{
  "identity": "john.doe@university.edu"
}
```

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `identity` | `string` | Yes | Email address OR institutional ID |

### Responses

#### 200 OK

```json
{
  "message": "A new OTP has been sent to the registered email address."
}
```

#### 400 Bad Request — Already Verified

```json
{
  "status": 400,
  "detail": "This account is already verified."
}
```

#### 404 Not Found

```json
{
  "status": 404,
  "detail": "Account not found."
}
```
