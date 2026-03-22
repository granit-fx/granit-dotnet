# Granit.Bundle.SaaS

Meta-package grouping Granit modules for multi-tenant SaaS applications:
tenant isolation, feature flags per commercial plan, and rate limiting.

Part of the [granit](https://granit-fx.dev) framework.

## Included packages

| Package | Role |
| --- | --- |
| `Granit.MultiTenancy` | Tenant isolation, `ICurrentTenant`, automatic resolution |
| `Granit.Features` | Feature management (Toggle/Numeric/Selection), plan-based resolution |
| `Granit.Features.EntityFrameworkCore` | EF Core feature store |
| `Granit.RateLimiting` | Rate limiting per tenant/plan |

## Installation

```bash
dotnet add package Granit.Bundle.SaaS
```

## Documentation

See the [multi-tenancy documentation](https://granit-fx.dev).
