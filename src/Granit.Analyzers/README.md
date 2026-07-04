# Granit.Analyzers

Roslyn analyzers enforcing Granit conventions: module boundaries,
zero-downtime migrations, security best practices, Entity Framework Core
usage rules, and Minimal API patterns.

Part of the [granit](https://granit-fx.dev) framework.

## Rules

### Architecture

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GRMOD001 | Error | Yes | Cross-module reference to internal type — use Contracts |

### Migrations

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GRMIGA001 | Error | — | DropColumn requires a Contract-phase annotation |
| GRMIGA002 | Error | — | RenameColumn is not zero-downtime safe |
| GRMIGA003 | Warning | — | AddColumn NOT NULL without a default value risks a table lock |
| GRMIGA004 | Warning | — | AlterColumn with a type change requires a Contract-phase annotation |

### Security

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GRSEC001 | Warning | Yes | Avoid direct DateTime/DateTimeOffset clock access — use IClock |
| GRSEC002 | Warning | Yes | Avoid Guid.NewGuid() — use IGuidGenerator |
| GRSEC003 | Error | — | Potential hardcoded secret detected |
| GRSEC004 | Warning | Yes | Avoid direct IResponseCookies access — use IGranitCookieManager |

### Entity Framework

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GREF001 | Warning | Yes | Use SaveChangesAsync() instead of SaveChanges() |

### API

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GRAPI001 | Warning | Yes | Use TypedResults instead of Results for OpenAPI |
| GRAPI002 | Warning | Yes | Use TypedResults.Problem() instead of BadRequest (RFC 7807) |

### Design

| Rule | Severity | CodeFix | Description |
| ---- | -------- | ------- | ----------- |
| GRENUM001 | Warning | Yes | Redundant sequential-from-zero explicit enum value — enums persist by name (ADR-059); skips `[Flags]` and `[PersistAsInt]` |

## Installation

```bash
dotnet add package Granit.Analyzers
```

## Documentation

See the [Granit documentation](https://granit-fx.dev).
