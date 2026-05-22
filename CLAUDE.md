# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Lumen is a .NET 10 ASP.NET Core Web API following a Clean Architecture layering. The repository is an early-stage scaffold: the project layout, dependencies, and `Program.cs` wiring exist, but most layers contain only empty `.gitkeep` folders waiting to be filled in.

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
- **Lumen.Infrastructure** — EF Core data access (`LumenDbContext`), repositories, infrastructure services. Depends on Application + Domain. Uses Npgsql (PostgreSQL); EF migrations live here.
- **Lumen.API** — controllers, middleware, composition root (`Program.cs`). Depends on Infrastructure + Application.

`tests/Lumen.Tests` is an xUnit project (Moq, `Microsoft.AspNetCore.Mvc.Testing`, coverlet). It currently references only `Lumen.Application`; add project references as you add tests for other layers.


<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
