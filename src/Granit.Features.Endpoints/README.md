# Granit.Features.Endpoints

Minimal API endpoints for Granit SaaS feature management: feature definitions (read-only), resolved values for the current tenant/plan context, and tenant-level override CRUD with value-type validation (Toggle, Numeric, Selection).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Features.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Features`
- `Granit.Http.ApiDocumentation`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions` (the module registers a `FeaturesFeatureProvider : IFeatureProvider`)

## Documentation

See the [full documentation](https://granit-fx.dev).
