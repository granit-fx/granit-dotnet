---
title: "ADR-002: Redis via StackExchange.Redis — Distributed Cache"
description: "StackExchange.Redis powers Granit's L2 distributed cache layer — selected for pub/sub invalidation, Lua scripting for atomic rate limiting, and HybridCache support."
sidebar:
  order: 2
  label: "002 - Redis Distributed Cache"
---

> **Date:** 2026-02-21
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Caching, Granit.Caching.StackExchangeRedis, Granit.Caching.Hybrid)

## Context

The Granit framework provides cache abstractions (`Granit.Caching`) and
requires a distributed cache backend for:

- **Performance**: reducing latency for frequent reads (settings, translations,
  templates, permissions)
- **Scalability**: shared cache across Kubernetes instances (sticky sessions
  impossible in an ISO 27001 context — high availability required)
- **Idempotency**: HTTP idempotency key storage
- **SignalR**: Redis backplane for real-time notifications

The choice of cache backend determines the implementation of
`Granit.Caching.StackExchangeRedis` and the L1+L2 pattern of `Granit.Caching.Hybrid`.

## Decision

**Redis** via **StackExchange.Redis** as the distributed cache backend (L2),
combined with `Microsoft.Extensions.Caching.Hybrid` for the L1+L2 pattern.

## Alternatives considered

### Option 1: Redis via StackExchange.Redis (selected)

- **License**: MIT (StackExchange.Redis)
- **Advantage**: de facto standard, native `IDistributedCache` integration,
  HybridCache support, Pub/Sub for invalidation, SignalR backplane

### Option 2: Memcached

- **Advantage**: simple, lightweight, performant for pure key-value
- **Disadvantage**: no Pub/Sub, no advanced data structures,
  no persistence, no SignalR backplane

### Option 3: NCache

- **Advantage**: .NET native solution, advanced topologies
- **Disadvantage**: commercial license, limited community,
  no standard HybridCache integration

### Option 4: Microsoft Garnet

- **Advantage**: Redis protocol compatible, superior performance
- **Disadvantage**: recent project (2024), no managed service, stability risk
  for ISO 27001 production use

## Justification

| Criterion | SE.Redis | Memcached | NCache | Garnet |
| --------- | -------- | --------- | ------ | ------ |
| Client license | MIT | Apache-2.0 | Freemium | MIT |
| IDistributedCache | Native MS | Third-party | Third-party | Compatible |
| HybridCache .NET 10 | Yes | No | No | Compatible |
| Pub/Sub | Yes | No | Yes | Yes |
| SignalR backplane | Yes (MS official) | No | No | Untested |
| Maturity | 10+ years | Mature | Mature | Recent |

## Consequences

### Positive

- Native integration with Microsoft DI (`IDistributedCache`, `HybridCache`)
- MIT client, stable and very widely adopted
- Pub/Sub for cache invalidation and SignalR backplane
- Transparent L1+L2 HybridCache pipeline via `Granit.Caching`

### Negative

- Redis is an additional infrastructure dependency to operate
- Redis 7.4+ license (SSPL): to monitor if self-hosted
- Complex object serialization requires a consistent strategy

## Re-evaluation conditions

This decision should be re-evaluated if:

- Microsoft Garnet reaches production maturity and offers a managed service
- The Redis license (SSPL) becomes problematic for self-hosted deployment
- Cache needs evolve toward a pattern incompatible with Redis
  (e.g. geographically distributed cache)

## References

- Initial commit: `76378865` (2026-02-21)
- StackExchange.Redis: <https://github.com/StackExchange/StackExchange.Redis>
