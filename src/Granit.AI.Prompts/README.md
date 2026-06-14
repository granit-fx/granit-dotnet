# Granit.AI.Prompts

The user-facing prompt catalogue behind the chat `/` picker (ADR-067) — distinct from the
code-first guardrail prompts owned by `Granit.AI.Tools`. A DB-backed, owned, multi-tenant store of
reusable prompts.

This package holds the domain: the `PromptTemplate` aggregate (`IOwnable` + `IMultiTenant`, name,
short description, content, icon + `HexColor`, version, `IsSystem`) and the `IPromptTemplateStore`
abstraction. Persistence is provided by `Granit.AI.Prompts.EntityFrameworkCore`.

Framework-seeded generic prompts are flagged `IsSystem` and owned by no user; a user's own prompts
are private (v1, sharing deferred to phase 2).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Prompts
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
