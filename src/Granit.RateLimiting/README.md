# Granit.RateLimiting

Per-tenant rate limiting for Granit APIs. Sliding window, fixed window, and token bucket algorithms via Redis Lua scripts. Plan-based quotas via Granit.Features integration. ASP.NET Core endpoint filter (429 + Retry-After) and Wolverine middleware.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.RateLimiting
```

## Dependencies

- `Granit`
- `Granit.Http.ExceptionHandling`
- `Granit.Features`
- `Granit.Users`

## Documentation

See the [full documentation](https://granit-fx.dev).
