# Granit.AI.Extraction.StackExchangeRedis

Distributed, Redis-backed `IAICallRateLimiter` for the Granit AI feature family
(`Granit.LanguageDetection.AI`, `Granit.Indexing.AI`, …).

## Why you need it

`Granit.AI.Extraction` ships an **in-memory** `IAICallRateLimiter` (a per-process
`ConcurrentDictionary` sliding window). In a multi-replica deployment that limiter is
**per pod**: with _N_ pods behind a load balancer, the advertised
`MaxAICallsPerHourPerTenant` ceiling is silently multiplied to **_N_ × cap**. An
attacker (or a runaway tenant) can therefore drive _N_× the intended LLM spend before a
single `throttled` metric fires — a denial-of-wallet gap.

This package replaces the in-memory limiter with a **Redis sliding window** (sorted set,
server-side `TIME`, atomic Lua admit-or-deny) so the per-tenant ceiling is enforced
**once across every replica**.

## Registration

The package shares the host's existing `IConnectionMultiplexer` — typically the one
registered by `Granit.Caching.StackExchangeRedis`. No second Redis connection.

### Module auto-wiring (default)

Referencing the package is enough: its module replaces the in-memory limiter **when an
`IConnectionMultiplexer` is already registered**. If none is present the package stays
inert and the safe in-memory default remains — so a transitive reference never breaks a
host that has no Redis.

```jsonc
// appsettings.json
{
  "AI": {
    "RateLimiting": {
      "Redis": {
        "Enabled": true,                          // default — set false to opt out
        "KeyPrefix": "granit:ai:ratelimit:myapp:", // isolate this app on a shared Redis
        "AllowOnRedisFailure": false              // default fail-closed (see below)
      }
    }
  }
}
```

### Explicit wiring

If your `IConnectionMultiplexer` is registered by a module that configures **after** this
one (DI registration order is not guaranteed across unrelated modules), wire it
explicitly:

```csharp
builder.Services.AddGranitAIExtractionRedisRateLimiter();
```

## Behaviour notes

- **Window:** fixed 1 hour, matching the in-memory limiter. The cap is caller-supplied
  per AI feature (`MaxAICallsPerHourPerTenant`, `MaxAutoTagCallsPerHourPerTenant`, …).
- **Key prefix:** every bucket key is `"{KeyPrefix}{{bucketKey}}"`. The `{…}` hash tag
  keeps all commands for one bucket on a single Redis Cluster slot, and `KeyPrefix`
  isolates this application's buckets from other Granit apps sharing the same Redis. A
  collision would let one app's calls count against another's ceiling.
- **Redis outage (`AllowOnRedisFailure`):** defaults to `false` — **fail closed**. A
  denied call makes the consumer gracefully fall through to its non-AI path (Trigram,
  lexical search, …), which protects the cost ceiling during an outage. Set `true` to
  favour availability (let calls through) over the wallet.

## Relationship to `Granit.RateLimiting`

This package intentionally **does not** build on `Granit.RateLimiting`. The ~20-line
sliding-window Lua here is duplicated on purpose.

`Granit.RateLimiting` has since been split into a framework-pure core (#2366), so the
original objection — that depending on it dragged ASP.NET Core into the AI path — no longer
holds. Reuse was reconsidered (#2364 follow-up) and **deliberately declined**:

- Its `RedisRateLimitCounterStore` is `internal`; reuse would have to go through the
  `IRateLimitCounterStore` DI seam, coupling this limiter's fail-open/closed behaviour to
  `GranitRateLimitingOptions` instead of its own `AIRateLimitingRedisOptions`.
- Even framework-pure, the core transitively pulls `Granit.Features` → `Granit.Caching` +
  `Granit.Localization` — i.e. the SaaS feature-flag, cache, and localization stacks — into
  any app that just wants a distributed AI call limiter. That is the wrong abstraction for
  a self-contained ~20-line algorithm that never changes.

Cheaper to keep the duplication than to take on that coupling. If a third Redis
sliding-window consumer appears, extract a minimal shared primitive (StackExchange.Redis
only) rather than reaching for `Granit.RateLimiting`.
