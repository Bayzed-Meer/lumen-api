---
name: feedback-migrations
description: Skip migrations during development; reset DB when done
metadata:
  type: feedback
---

Skip creating EF migrations during feature development. Schema changes go directly into entity/configuration files. When development is complete, the database will be reset and a single migration applied.

**Why:** We're in active development; migrations add churn with no benefit until the feature is stable.

**How to apply:** Never run `dotnet ef migrations add` during implementation. Only update entity classes and EF configuration files.
