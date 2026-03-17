# Granit.Bundle.Essentials

Meta-package grouping the essential Granit modules for a minimal API.
Install this single package instead of adding 9 individual references.

Part of the [Granit](https://granit-fx.dev) framework.

## Included packages

| Package | Role |
| --- | --- |
| `Granit.Core` | Module system, shared domain types |
| `Granit.Timing` | `IClock`, `TimeProvider` |
| `Granit.Guids` | `IGuidGenerator`, sequential GUIDs |
| `Granit.Security` | `ICurrentUserService` |
| `Granit.Validation` | FluentValidation integration |
| `Granit.Persistence` | EF Core audit interceptors, soft delete |
| `Granit.Observability` | Serilog + OpenTelemetry |
| `Granit.Http.ExceptionHandling` | RFC 7807 ProblemDetails |
| `Granit.Diagnostics` | Health checks, readiness probes |

## Installation

```bash
dotnet add package Granit.Bundle.Essentials
```

## Documentation

See the [Getting Started guide](https://granit-fx.dev).
