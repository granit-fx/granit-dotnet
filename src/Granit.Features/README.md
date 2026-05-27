# Granit.Features

Framework-pure SaaS Feature Management core for Granit. Multi-level resolution
(Default → Plan → Tenant), hybrid cache, `IFeatureChecker`, `IFeatureLimitGuard` for numeric
limits.

This package is **HTTP-agnostic**. Pick the binding for your transport:

- **`Granit.Http.Features`** — Minimal-API endpoint filter (`.RequiresFeature("name")`, 403 when
  disabled).
- **`Granit.Features.Wolverine`** — Wolverine message middleware + `[RequiresFeature]` attribute.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Features
```

## Dependencies

- `Granit.Caching`
- `Granit.Localization`

## Documentation

See the [full documentation](https://granit-fx.dev).
