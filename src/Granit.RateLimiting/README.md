# Granit.RateLimiting

Framework-pure, per-tenant rate limiting core for Granit. Sliding window, fixed window, and token
bucket algorithms via Redis Lua scripts, with an in-memory fallback. Plan-based quotas via
`Granit.Features` integration.

This package is **HTTP-agnostic** — it contains the counter stores, algorithms, quota providers, and
the `TenantPartitionedRateLimiter`. Pick the binding for your transport:

- **`Granit.Http.RateLimiting`** — ASP.NET Core endpoint filter (`.RequireGranitRateLimiting("policy")`,
  429 + `Retry-After`).
- **`Granit.RateLimiting.Wolverine`** — Wolverine message middleware + `[RateLimited]` attribute.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.RateLimiting
```

## Dependencies

- `Granit`
- `Granit.Features`

## Documentation

See the [full documentation](https://granit-fx.dev).
