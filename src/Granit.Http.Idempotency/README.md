# Granit.Http.Idempotency

HTTP idempotency middleware for Granit APIs. Stripe-style Idempotency-Key header, a first-class
`IIdempotencyStore` contract with atomic state transitions, and an ISO 27001-compliant audit trail.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Idempotency
```

## Quick start

Two steps are required:

1. **Register the module** — adds `IdempotencyMiddleware` + DI (the module does
   not add middleware to the pipeline by itself):

   ```csharp
   [DependsOn(typeof(GranitHttpIdempotencyModule))]
   public sealed class AppHostModule : GranitModule { }
   ```

2. **Wire the middleware** in `Program.cs`, **after** authentication so
   `ICurrentUserService` / `ICurrentTenant` are populated when it runs:

   ```csharp
   app.UseAuthentication();
   app.UseAuthorization();
   app.UseGranitIdempotency();   // must run after auth
   ```

## Storage

`IIdempotencyStore` expresses the state machine's atomic transitions explicitly:
`TryAcquireAsync` (create-if-absent with TTL), `CompleteAsync` / `TombstoneAsync`
(set-if-present), `GetAsync`, `DeleteAsync` — plus `IsDistributed` and `BackendName`
on the contract itself, so the startup guard works for any implementation.

Two stores ship with the framework:

| Store | Package | Distributed | Use |
| ----- | ------- | ----------- | --- |
| `InMemoryIdempotencyStore` | this package (default) | No | Development / single replica |
| `RedisIdempotencyStore` | `Granit.Http.Idempotency.StackExchangeRedis` | Yes | Production |

The in-memory default is **per-process**: under multiple replicas the same
`Idempotency-Key` routed to two pods executes twice. A startup guard therefore fails
the host outside Development unless `Http:Idempotency:AllowInMemoryStore` is
explicitly set (genuinely single-instance deployments only). Install the Redis
provider for production — it replaces the in-memory store deterministically and
encrypts entries at rest (AES-256-GCM).

Keys are already fully namespaced by the middleware
(`{prefix}:{tenant}:{user}:{method}:{route}:{sha256(key)}`) — custom store
implementations must **not** re-namespace by tenant; only an application-scoped
instance prefix is acceptable.

## Documentation

See the [full documentation](https://granit-fx.dev).
