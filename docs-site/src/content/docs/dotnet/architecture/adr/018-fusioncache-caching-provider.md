---
title: "ADR-018: FusionCache — Hybrid Caching Provider"
description: "FusionCache replaces ICacheService<T> and Granit.Caching.Hybrid as the caching provider — selected for backplane, fail-safe, factory timeouts, and native OpenTelemetry."
sidebar:
  order: 18
  label: "018 - FusionCache Caching Provider"
---

> **Date:** 2026-03-20
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Caching, Granit.Authorization, Granit.Settings, Granit.Features, Granit.Localization, Granit.ReferenceData)
> **Supersedes:** ADR-002 (Redis) partially — Redis remains as L2 backend, but the caching layer changes from `HybridCache` to FusionCache.

## Context

The Granit caching stack (`Granit.Caching` + `Granit.Caching.Hybrid`) has four
critical gaps in a multi-pod Kubernetes production environment:

1. **No backplane** — when a cache entry is invalidated on pod A,
   pods B and C continue serving stale L1 data for up to 30 seconds
   (the `LocalCacheExpiration` TTL). For permissions and feature flags,
   this creates a security and consistency window.

2. **No generalized fail-safe** — if the cache factory (DB query) fails
   during a cache miss, the request fails with a 500 error. Stale-on-error
   is only implemented ad-hoc in `CachedUserLookupService` via manual
   try/catch. Authorization, Settings, and Features have no protection.

3. **No factory timeouts** — a slow DB query blocks the entire HTTP request
   pipeline. There is no mechanism to serve stale data while the factory
   completes in the background.

4. **No OpenTelemetry metrics** — cache operations (hits, misses, fail-safe
   activations, factory durations) are invisible to the monitoring stack.
   Only `Debug`-level structured logs exist.

Additionally, the custom `ICacheService<T>` abstraction limits access to
advanced caching features and adds a layer of indirection with no production
consumers to protect.

## Decision

**Adopt [FusionCache](https://github.com/ZiggyCreatures/FusionCache)** as the
caching provider. Drop `ICacheService<T>` entirely — consumers inject
`IFusionCache` directly.

### Package changes

| Package | Action |
| ------- | ------ |
| `Granit.Caching` | **Consolidated** — FusionCache wiring, backplane, OpenTelemetry, config, encryption (`EncryptingFusionCacheSerializer`). |
| `Granit.Caching.StackExchangeRedis` | **Unchanged** — Redis `IDistributedCache` + health check |
| `Granit.Caching.Hybrid` | **Deleted** |

### NuGet dependencies in Granit.Caching (all MIT license)

- `ZiggyCreatures.FusionCache`
- `ZiggyCreatures.FusionCache.Serialization.SystemTextJson`
- `ZiggyCreatures.FusionCache.OpenTelemetry`

### NuGet dependencies in Granit.Caching.StackExchangeRedis (all MIT license)

- `ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis`

## Alternatives considered

### Option 1: FusionCache as direct provider (selected)

- **License**: MIT
- **Advantage**: backplane, fail-safe, factory timeouts, eager refresh,
  tagging, OpenTelemetry, auto-recovery — all battle-tested over 4 years
- **Advantage**: `IFusionCache` is a rich, well-designed API — no need
  for a custom abstraction

### Option 2: Implement missing features in Granit.Caching

- **Advantage**: zero external dependency
- **Disadvantage**: estimated 3-4 weeks of development for backplane,
  fail-safe, factory timeouts, and metrics — with high risk of
  subtle distributed caching bugs (race conditions, edge cases)

### Option 3: Keep ICacheService\<T\> as abstraction over FusionCache

- **Advantage**: provider-agnostic abstraction
- **Disadvantage**: hides FusionCache features (fail-safe per-entry,
  tags, `ExpireAsync`, eager refresh). Since there are no production
  consumers and no foreseeable provider switch, the abstraction
  layer adds cost with no benefit.

## Justification

| Criterion | FusionCache | Granit.Caching.Hybrid | Implement ourselves |
| --------- | ----------- | --------------------- | ------------------- |
| Backplane (cross-pod) | Native (Redis pub/sub) | None (30s TTL) | 2+ weeks |
| Fail-safe (stale on error) | Native, per-entry | None | 1+ week |
| Factory timeouts | Soft + hard | None | 1 week |
| Eager refresh | Background at threshold | None | 1 week |
| OpenTelemetry | Native package | None | 1 week |
| Stampede protection | Native | Native (HybridCache) | Custom SemaphoreSlim |
| Encryption | Via serializer decorator | Not supported | Native (AES) |
| Maturity | 4+ years, 4k+ stars | .NET 9 (recent) | Untested |
| Maintenance | Community + Microsoft endorsed | Microsoft | Granit team |

## Consequences

### Positive

- **Resilience**: all cache consumers (permissions, settings, features,
  localization, reference data) gain fail-safe automatically — no code changes
  needed per module beyond the initial migration
- **Consistency**: backplane ensures cross-pod L1 invalidation in real-time
  (vs 30s staleness)
- **Performance**: eager refresh eliminates cold cache hits; factory timeouts
  prevent slow queries from blocking requests
- **Observability**: native OpenTelemetry metrics fill the cache monitoring
  blind spot
- **Simplicity**: `IFusionCache` is a single, well-documented API — no custom
  abstraction to learn or maintain
- **Invalidation**: `ExpireAsync` replaces `RemoveAsync` in Wolverine handlers —
  entries are marked stale (fail-safe fallback) rather than deleted

### Negative

- **New dependency**: 4 FusionCache NuGet packages (MIT license, compatible
  with Apache 2.0)
- **Sliding expiration dropped**: FusionCache does not support sliding
  expiration natively. Mitigated by absolute `Duration` + `EagerRefreshThreshold`
  which is semantically equivalent and prevents indefinite stale retention
- **L1 unencrypted**: `EncryptingFusionCacheSerializer` only encrypts L2
  (Redis). L1 (pod-local RAM) stores objects in cleartext. This matches the
  current `IMemoryCache` threat model — access to pod RAM requires pod access.
  Documented as acceptable for ISO 27001 (encryption at rest = Redis, encryption
  in transit = TLS).
- **IMemoryCache behavioral change**: Localization and ReferenceData transition
  from reference-based caching (`IMemoryCache`) to value-based caching
  (JSON deserialized copies). This is actually a safety improvement
  (prevents accidental mutation) but requires verifying JSON serializability
  of cached types.

## Encryption threat model

| Layer | Storage | Encrypted | Justification |
| ----- | ------- | --------- | ------------- |
| L1 | Pod-local RAM | No | Same as `IMemoryCache` — requires pod access to read |
| L2 | Redis | Yes (AES-256-GCM) | `EncryptingFusionCacheSerializer` encrypts before write |
| Transit | Network | Yes (TLS) | Redis TLS configuration (infrastructure) |
| Backplane | Redis pub/sub | No (keys only) | Only cache keys are published, not values |

## Re-evaluation conditions

This decision should be re-evaluated if:

- FusionCache is abandoned or its license changes to a non-permissive model
- .NET introduces a native caching framework with equivalent features
  (backplane, fail-safe, factory timeouts)
- Cache needs evolve toward a pattern incompatible with FusionCache
  (e.g., strongly-typed named caches with compile-time safety)

## References

- Epic: [#522](https://github.com/granit-fx/granit-dotnet/issues/522)
- FusionCache: <https://github.com/ZiggyCreatures/FusionCache>
- FusionCache docs: <https://github.com/ZiggyCreatures/FusionCache/tree/main/docs>
- Superseded: ADR-002 (Redis — L2 backend unchanged, caching layer replaced)
