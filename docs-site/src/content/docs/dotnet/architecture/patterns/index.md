---
title: "Pattern Library — 57 Design Patterns for .NET"
description: Catalogue of design patterns implemented in Granit — GoF patterns, architecture patterns, cloud/SaaS patterns, concurrency, security, and .NET idioms.
sidebar:
  label: Pattern Library
  order: 0
  badge:
    text: "57"
    variant: note
---

A catalogue of design patterns used in the Granit framework, organized by category.

Each pattern documents the general concept, how it is implemented in Granit,
and references to the actual source files where the pattern is applied.

## Architecture patterns

| Pattern | Description |
| ------- | ----------- |
| [Module System](./module-system/) | Topological loading with `[DependsOn]` |
| [Hexagonal Architecture](./hexagonal-architecture/) | Ports and Adapters for infrastructure decoupling |
| [Layered Architecture](./layered-architecture/) | Domain / Application / Infrastructure separation |
| [Middleware Pipeline](./middleware-pipeline/) | Dual ASP.NET Core + Wolverine pipeline |
| [Event-Driven](./event-driven/) | IDomainEvent (local) + IIntegrationEvent (durable) |
| [REPR](./repr/) | Minimal API Request-Endpoint-Response |
| [CQRS](./cqrs/) | IReader / IWriter separation, ArchUnitNET enforcement |
| [Vertical Slice Architecture](./vertical-slice-architecture/) | Feature-organized code, per-use-case slices |
| [Aggregate Root](./ddd-aggregate-roots/) | DDD aggregate roots for business invariants and domain events |
| [Anti-Corruption Layer](./anti-corruption-layer/) | Isolation of Keycloak, S3, Brevo, FCM via internal DTOs |

## Cloud and SaaS patterns

| Pattern | Description |
| ------- | ----------- |
| [Multi-Tenancy](./multi-tenancy/) | 3 isolation strategies, soft dependency, async propagation |
| [Feature Flags](./feature-flags/) | Multi-level resolution Tenant to Plan to Default |
| [Transactional Outbox](./transactional-outbox/) | Atomic event publishing via Wolverine Outbox |
| [Idempotency](./idempotency/) | Stripe-style HTTP idempotency with state machine |
| [Pre-Signed URL](./pre-signed-url/) | Direct-to-cloud S3 upload/download |
| [Sidecar / Behavior](./sidecar-behavior/) | Context propagation via Wolverine Behaviors |
| [Circuit Breaker and Retry](./circuit-breaker-retry/) | Standard resilience + Wolverine RetryWithCooldown |
| [Cache-Aside](./cache-aside/) | Double-check locking + HybridCache L1/L2 |
| [Rate Limiting](./rate-limiting/) | Per-tenant rate limiting with dynamic quotas |
| [Saga / Process Manager](./saga-process-manager/) | GDPR export, import/export orchestrators |
| [Fan-Out](./fan-out/) | Wolverine cascade for notifications and webhooks |
| [Claim Check](./claim-check/) | Soft dependency IClaimCheckStore for large payloads |
| [Bulkhead Isolation](./bulkhead-isolation/) | Queue isolation, parallelism, tenant quotas |

## GoF behavioral patterns

| Pattern | Description |
| ------- | ----------- |
| [Strategy](./strategy/) | TenantIsolationStrategy, IBlobKeyStrategy, IStringEncryptionProvider |
| [Chain of Responsibility](./chain-of-responsibility/) | TenantResolverPipeline, blob validation |
| [Command](./command/) | SendWebhookCommand, RunMigrationBatchCommand |
| [Template Method](./template-method/) | GranitModule lifecycle, GranitValidator |
| [State Machine](./state-machine/) | IdempotencyState, BlobStatus |
| [Observer / Event](./observer-event/) | Wolverine implicit event subscription |
| [Mediator](./mediator/) | Wolverine message bus |
| [Null Object](./null-object/) | NullTenantContext, NullCacheValueEncryptor |

## GoF creational patterns

| Pattern | Description |
| ------- | ----------- |
| [Factory Method](./factory-method/) | VaultClientFactory, DbContext tenant factories |
| [Singleton](./singleton/) | AsyncLocal singletons, NullTenantContext.Instance |
| [Builder](./builder/) | Fluent `AddGranit*()` extensions |

## GoF structural patterns

| Pattern | Description |
| ------- | ----------- |
| [Adapter](./adapter/) | TypedKeyCacheServiceAdapter, S3BlobClient |
| [Decorator](./decorator/) | DistributedCacheService, CachedLocalizationOverrideStore |
| [Proxy](./proxy/) | FilterProxy for EF Core, Interceptors |
| [Facade](./facade/) | DefaultBlobStorage, GranitExceptionHandler |
| [Composite](./composite/) | Auditable entity hierarchy |

## Data patterns

| Pattern | Description |
| ------- | ----------- |
| [Repository](./repository/) | Store interfaces + EF Core / InMemory implementations |
| [Soft Delete](./soft-delete/) | ISoftDeletable + SoftDeleteInterceptor (GDPR) |
| [Data Filtering](./data-filtering/) | IDataFilter with ImmutableDictionary AsyncLocal |
| [Unit of Work](./unit-of-work/) | Implicit DbContext + interceptor chain |
| [Specification](./specification/) | QueryDefinition whitelist-first, expression trees |

## Concurrency patterns

| Pattern | Description |
| ------- | ----------- |
| [Scope / Context Manager](./scope-context-manager/) | `using` pattern for context restoration |
| [Copy-on-Write](./copy-on-write/) | ImmutableDictionary for thread-safe state |
| [Double-Check Locking](./double-check-locking/) | Anti-stampede on cache miss |

## .NET idiom patterns

| Pattern | Description |
| ------- | ----------- |
| [Expression Trees](./expression-trees/) | Dynamic EF Core query filter construction |
| [Marker Interface](./marker-interface/) | ISoftDeletable, IMultiTenant, IDomainEvent |
| [Options Pattern](./options-pattern/) | 93 Options classes, ValidateOnStart |

## Security patterns

| Pattern | Description |
| ------- | ----------- |
| [Claims-Based Identity](./claims-based-identity/) | JWT Keycloak + dynamic RBAC |
| [Guard Clause](./guard-clause/) | Systematic fail-fast, semantic exceptions |

## AI patterns

| Pattern | Description |
| ------- | ----------- |
| [Retrieval-Augmented Generation](./rag/) | Vector retrieval + LLM grounding (`AI.VectorData`) |
| [AI Workspace](./ai-workspace/) | Named, per-tenant provider configuration (`IAIChatClientFactory`) |
| [Graceful AI Fallback](./ai-fallback/) | Timeout + deterministic baseline (`Querying.AI`, `DataExchange.AI`) |
| [Structured Output](./structured-output/) | Type-safe LLM response via JSON schema (`CompleteAsync<T>`) |

## Granit-specific variants

| Pattern | Description |
| ------- | ----------- |
| [Granit Variants](./granit-variants/) | 10 hybrid patterns unique to Granit |
