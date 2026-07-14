# Granit.Http.Idempotency.StackExchangeRedis

Redis store for `Granit.Http.Idempotency`. Replaces the in-memory `IIdempotencyStore`
with atomic Redis transitions — acquire is `SET NX PX`, complete/tombstone are
`SET XX PX` (an existence guard is sufficient: only the lock winner ever writes the
terminal state, and `ExecutionTimeout < InProgressTtl` guarantees it writes before the
lock can expire, so no Lua script is needed). Reuses `IConnectionMultiplexer` from
`Granit.Caching.StackExchangeRedis` when co-registered. TLS enforced by default.

Entries are **encrypted at rest unconditionally** (AES-256-GCM via the
`Granit.Caching` `ICacheValueEncryptor` pipeline): a replayable entry carries the
original response — potentially PII or bearer tokens — and must never sit in plaintext
in a Redis snapshot or AOF file. Provide a base64 256-bit `Cache:Encryption:Key`;
outside Development the host refuses to start without one.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Idempotency.StackExchangeRedis
```

## Quick start

```csharp
[DependsOn(typeof(GranitHttpIdempotencyStackExchangeRedisModule))]
public sealed class AppHostModule : GranitModule { }
```

```jsonc
// appsettings.json
{
  "Http": {
    "Idempotency": {
      "Redis": {
        "Configuration": "redis-service:6379",
        "InstanceName": "dd:",
        "RequireTls": true
      }
    }
  },
  "Cache": {
    "Encryption": {
      "Key": "<base64 256-bit key from Vault>"
    }
  }
}
```

Optional readiness/startup health check:

```csharp
builder.Services.AddHealthChecks().AddGranitRedisIdempotencyHealthCheck();
```

## Dependencies

- `Granit.Http.Idempotency`
- `Granit.Caching.StackExchangeRedis`
- `StackExchange.Redis`

## Documentation

See the [full documentation](https://granit-fx.dev).
