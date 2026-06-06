# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Lumen is a .NET 10 ASP.NET Core Web API following a Clean Architecture layering. Identity and admin seeding are set up; most domain layers are still empty and ready to be filled in.

## Commands

```bash
dotnet build Lumen.slnx                          # build the whole solution
dotnet run --project src/Lumen.API               # run the API (http profile -> http://localhost:5148)
dotnet test                                      # run all tests
dotnet test --filter "FullyQualifiedName~ClassName"   # run a single test class/method
dotnet test --collect:"XPlat Code Coverage"      # run tests with coverage (coverlet)

dotnet ef migrations add <Name> --project src/Lumen.Infrastructure --startup-project src/Lumen.API
dotnet ef database update --project src/Lumen.Infrastructure --startup-project src/Lumen.API
```

## Architecture

Four projects under `src/`, with dependencies pointing inward (Clean Architecture):

- **Lumen.Domain** — entities, value objects, enums, domain exceptions. No dependencies.
- **Lumen.Application** — DTOs, service interfaces/implementations, AutoMapper profiles. Depends on Domain.
- **Lumen.Infrastructure** — EF Core data access (`AppDbContext`), repositories, infrastructure services, Identity types. Depends on Application + Domain. Uses Npgsql (PostgreSQL); EF migrations live here.
- **Lumen.API** — controllers, middleware, composition root (`Program.cs`). Depends on Infrastructure + Application.

`tests/Lumen.Tests` is an xUnit project (Moq, `Microsoft.AspNetCore.Mvc.Testing`, coverlet). It currently references only `Lumen.Application`; add project references as you add tests for other layers.

## Identity & Auth

- **Package**: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` in `Lumen.Infrastructure`
- **User entity**: `ApplicationUser : IdentityUser` lives in `src/Lumen.Infrastructure/Identity/` — never in Domain or Application
- **DbContext**: `AppDbContext : IdentityDbContext<ApplicationUser>` — call `base.OnModelCreating` before `ApplyConfigurationsFromAssembly`
- **Registration**: `AddIdentityCore<ApplicationUser>` (not `AddIdentity`) — JWT API, no cookies or sign-in manager
- **User management abstraction**: `IIdentityService` in Application, `IdentityService` in Infrastructure — writes go through `UserManager`, complex reads go through `AppDbContext`; never a `UserRepository` (UserManager already is the user repository)
- **Admin bootstrap**: `AdminSeeder` runs at startup via a scoped DI scope in `Program.cs` before `app.Run()`; idempotent — checks DB first

## Secrets & Configuration

- Never put passwords or credentials in `appsettings.json` — leave them empty or omit the key entirely
- Dev credentials: `dotnet user-secrets set` — stored outside the repo at `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
- Production credentials: environment variables using `__` as the key separator (e.g. `AdminSeed__Password`)
- User secrets only load when `ASPNETCORE_ENVIRONMENT=Development` — `launchSettings.json` sets this automatically for `dotnet run`


<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at `specs/002-password-management/plan.md`.
<!-- SPECKIT END -->
