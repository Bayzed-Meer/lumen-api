# Data Model: User Authentication & Role-Based Account Management

**Branch**: `001-user-auth` | **Date**: 2026-05-23

## Entity Overview

| Entity | Layer | Reason |
|--------|-------|--------|
| `UserRole` | Domain (enum) | Pure enum, no dependencies |
| `Student` | Domain | Business concept — holds a student's institutional ID |
| `Faculty` | Domain | Business concept — holds a faculty member's institutional ID |
| `Librarian` | Domain | Business concept — holds a librarian's institutional ID |
| `OtpRecord` | Domain | Business concept — the verification code tied to a user account |
| `ApplicationUser` | Infrastructure (Identity) | Extends `IdentityUser` — a framework type; cannot live in Domain |
| `RefreshToken` | Infrastructure | JWT token management mechanism, not a business concept |

Domain entities are plain C# classes with no EF Core or Identity dependencies. The FK to `AspNetUsers` is stored as a `string UserId` property — just a value, no navigation property. EF Core relationship configuration (`IEntityTypeConfiguration<T>`) lives in `Lumen.Infrastructure/Data/Configurations/` and wires up the FK there.

---

## Domain Layer

### `UserRole` Enum — `Lumen.Domain/Enums/UserRole.cs`

```csharp
public enum UserRole
{
    Admin,
    Librarian,
    Student,
    Faculty
}
```

---

### `Student` — `Lumen.Domain/Entities/Student.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `string` | Required, unique | FK value pointing to `AspNetUsers.Id`; wired up in EF config |
| `InstitutionalId` | `string` | Required, max 50 | Unique across all student accounts; enforced by DB index |

No EF Core attributes. No navigation properties. No framework dependencies.

---

### `Faculty` — `Lumen.Domain/Entities/Faculty.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `string` | Required, unique | FK value pointing to `AspNetUsers.Id` |
| `InstitutionalId` | `string` | Required, max 50 | Unique across all faculty accounts |

---

### `Librarian` — `Lumen.Domain/Entities/Librarian.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `string` | Required, unique | FK value pointing to `AspNetUsers.Id` |
| `InstitutionalId` | `string` | Required, max 50 | Unique across all librarian accounts |

---

### `OtpRecord` — `Lumen.Domain/Entities/OtpRecord.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `string` | Required | FK value pointing to `AspNetUsers.Id` |
| `CodeHash` | `string` | Required | SHA-256 of the 6-digit code |
| `IssuedAt` | `DateTimeOffset` | Required | |
| `ExpiresAt` | `DateTimeOffset` | Required | `IssuedAt + 10 minutes` |
| `IsInvalidated` | `bool` | Default: `false` | Set to `true` on resend or successful verify |
| `FailedAttempts` | `int` | Default: `0` | Incremented on wrong code; locked at 5 |

**Active OTP query**: `!IsInvalidated && ExpiresAt > DateTimeOffset.UtcNow`

**Locked OTP query**: `FailedAttempts >= 5 && !IsInvalidated`

**Rules**:
- Only one active OTP per user at any time (enforced at application level on create/resend)
- Expired OTPs are kept as audit trail; filtered out via `ExpiresAt`

---

## Infrastructure Layer — Identity

### `ApplicationUser` — `Lumen.Infrastructure/Identity/ApplicationUser.cs`

Must stay in Infrastructure because it extends `IdentityUser`, a framework type. Domain must remain dependency-free.

Extends `IdentityUser` (which provides `Id`, `Email`, `NormalizedEmail`, `PasswordHash`, `UserName`, `SecurityStamp`, etc.).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `FirstName` | `string` | Required, max 100 | |
| `LastName` | `string` | Required, max 100 | |
| `Role` | `UserRole` | Required | Stored as int (EF default for enums) |
| `IsVerified` | `bool` | Default: `false` | Flipped to `true` on OTP confirmation |
| `CreatedAt` | `DateTimeOffset` | Required, set at creation | |

**Constraints**:
- Email uniqueness enforced by `RequireUniqueEmail = true` on Identity (NormalizedEmail unique index)
- Admin users do NOT have a profile record (no institutional ID)

---

## Infrastructure Layer — Auth Entities

### `RefreshToken` — `Lumen.Infrastructure/Entities/RefreshToken.cs`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `string` | FK → `AspNetUsers.Id` | |
| `TokenHash` | `string` | Required, **unique index** | SHA-256 of the raw token value |
| `ExpiresAt` | `DateTimeOffset` | Required | `CreatedAt + 7 days` |
| `IsRevoked` | `bool` | Default: `false` | |
| `ReplacedByTokenId` | `Guid?` | FK → `RefreshToken.Id`, nullable | Forms a family chain for reuse detection |
| `CreatedAt` | `DateTimeOffset` | Required | |

**Reuse detection**: if a token with `IsRevoked = true` is presented, revoke all tokens for that user to force re-login.

---

## EF Core Configuration

Domain entities have no EF Core attributes. Their table mappings, FK relationships, indexes, and cascade rules are defined in Infrastructure via `IEntityTypeConfiguration<T>`:

```
Lumen.Infrastructure/Data/Configurations/
├── StudentConfiguration.cs     (FK to AspNetUsers, unique index on InstitutionalId)
├── FacultyConfiguration.cs
├── LibrarianConfiguration.cs
├── OtpRecordConfiguration.cs          (FK to AspNetUsers)
└── RefreshTokenConfiguration.cs       (FK to AspNetUsers, self-ref FK, unique index on TokenHash)
```

All configurations are picked up automatically by `ApplyConfigurationsFromAssembly` in `AppDbContext`.

New `DbSet<T>` properties to add:

```csharp
public DbSet<Student> Students => Set<Student>();
public DbSet<Faculty> Faculty => Set<Faculty>();
public DbSet<Librarian> Librarians => Set<Librarian>();
public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

---

## Entity Relationship Diagram

```
AspNetUsers (ApplicationUser)       [Infrastructure]
  │
  ├──[0..1] Student          [Domain — UserId string FK]
  ├──[0..1] Faculty          [Domain — UserId string FK]
  ├──[0..1] Librarian        [Domain — UserId string FK]
  ├──[0..*] OtpRecord               [Domain — UserId string FK]
  └──[0..*] RefreshToken            [Infrastructure — UserId FK]
                │
                └──[0..1] RefreshToken.ReplacedByToken  (self-referencing FK)

```

---

## Migration Strategy

One new EF Core migration named `AddAuthEntities`:

```bash
dotnet ef migrations add AddAuthEntities \
  --project src/Lumen.Infrastructure \
  --startup-project src/Lumen.API
```

This migration will:
1. Add columns `FirstName`, `LastName`, `Role`, `IsVerified`, `CreatedAt` to `AspNetUsers`
2. Create tables: `Students`, `Faculty`, `Librarians`, `OtpRecords`, `RefreshTokens`
3. Create unique indexes: `Students.InstitutionalId`, `Faculty.InstitutionalId`, `Librarians.InstitutionalId`, `RefreshTokens.TokenHash`

**Seeded data**: `AdminSeeder` must set `FirstName`, `LastName`, `Role`, and `IsVerified = true` on the admin `ApplicationUser` before calling `UserManager.CreateAsync`.
