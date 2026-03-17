# Granit.Http.OutputCaching.StackExchangeRedis

Redis output cache store for `Granit.Http.OutputCaching`. Replaces in-memory `IOutputCacheStore`
with Redis via `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis`. Reuses
`IConnectionMultiplexer` from `Granit.Caching.StackExchangeRedis` when co-registered.
TLS enforced by default.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.OutputCaching.StackExchangeRedis
```

## Dependencies

- `Granit.Http.OutputCaching`
- `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis`
- `StackExchange.Redis`

## Documentation

See the [full documentation](https://granit-fx.dev).
