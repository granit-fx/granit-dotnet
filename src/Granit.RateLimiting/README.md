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

## Configuration

Policies are bound from the `RateLimiting` section (`GranitRateLimitingOptions`):

```jsonc
{
  "RateLimiting": {
    "Enabled": true,                          // default true
    "KeyPrefix": "rl",                        // counter-key prefix
    "FallbackOnCounterStoreFailure": "Deny",  // Allow | Deny (default Deny)
    "Policies": {
      "uploads": {
        "Algorithm": "SlidingWindow",  // SlidingWindow | FixedWindow | TokenBucket | Concurrency
        "PartitionBy": "Tenant",       // Tenant | TenantAndIp | Ip | User | TenantAndUser
        "PermitLimit": 100,
        "Window": "00:01:00"
      }
    }
  }
}
```

Each named policy is referenced by the transport binding (e.g.
`.RequireGranitRateLimiting("uploads")` in `Granit.Http.RateLimiting`).

## Documentation

See the [full documentation](https://granit-fx.dev).
