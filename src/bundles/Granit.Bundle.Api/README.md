# Granit.Bundle.Api

Meta-package for building a complete REST API with Granit.
Includes everything from `Granit.Bundle.Essentials` plus API-specific modules.

Part of the [granit](https://granit-fx.dev) framework.

## Included packages

Everything in `Granit.Bundle.Essentials`, plus:

| Package | Role |
| --- | --- |
| `Granit.Http.ApiVersioning` | Asp.Versioning integration |
| `Granit.Http.ApiDocumentation` | Scalar OpenAPI documentation |
| `Granit.Http.Cors` | CORS policy configuration |
| `Granit.Http.Idempotency` | Idempotency-Key middleware |
| `Granit.Localization` | i18n (17 cultures) |
| `Granit.Localization.EntityFrameworkCore` | Localization EF Core store |
| `Granit.Caching` | Distributed caching abstractions |

## Installation

```bash
dotnet add package Granit.Bundle.Api
```

## Documentation

See the [Getting Started guide](https://granit-fx.dev).
