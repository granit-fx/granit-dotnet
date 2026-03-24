---
title: "Granit-Specific Design Pattern Variants"
description: 10 hybrid patterns unique to Granit — adaptations of classic patterns for GDPR, multi-tenancy, and Wolverine
sidebar:
  label: Granit-Specific Variants
  order: 55
---

Some patterns in Granit are variants or hybrids of classic patterns, adapted to
the framework's specific constraints (GDPR/ISO 27001, multi-tenancy, Wolverine).
This page catalogues the 10 main "in-house" variants.

## 1. AsyncLocal Singleton (thread-safe context)

**Classic pattern**: Singleton with a single global instance.

**Granit variant**: `static readonly AsyncLocal<T>` — a singleton state
*per async flow*, thread-safe without locks.

```csharp
// src/Granit.MultiTenancy/CurrentTenant.cs
private static readonly AsyncLocal<TenantInfo?> _current = new();
```

The state is inherited by child flows (`Task.Run`) but modifiable independently
thanks to copy-on-write semantics.

## 2. ImmutableDictionary Copy-on-Write

**Classic pattern**: Copy-on-Write on data structures.

**Granit variant**: `AsyncLocal<ImmutableDictionary<Type, bool>>` prevents
mutations in a child flow from propagating to the parent flow.

```csharp
// src/Granit/DataFiltering/DataFilter.cs
_state.Value = _state.Value!.SetItem(typeof(TFilter), false);
// New dictionary created — the original remains intact
```

This specifically solves the "AsyncLocal trap" where a shared `Dictionary<T>`
would see child mutations affecting the parent.

## 3. Null Object as Soft Dependency

**Classic pattern**: Null Object with a single interface.

**Granit variant**: `NullTenantContext` is the **DI default**, replaced only when
`Granit.MultiTenancy` is installed. All modules access `ICurrentTenant` via
`Granit.MultiTenancy` without a direct dependency on `Granit.MultiTenancy`.

The Null Object is architecturally a **soft dependency** mechanism — an optional
package does not break packages that implicitly depend on it.

## 4. Enum-based Strategy Selection

**Classic pattern**: Strategy with interface + factory.

**Granit variant**: `TenantIsolationStrategy` is a simple enum. The factory uses
a switch expression to select the implementation.

```csharp
TenantIsolationStrategy.SharedDatabase => new SharedDatabaseDbContextFactory(...),
TenantIsolationStrategy.SchemaPerTenant => new TenantPerSchemaDbContextFactory(...),
TenantIsolationStrategy.DatabasePerTenant => new TenantPerDatabaseDbContextFactory(...),
```

Simpler than an abstract factory when the number of strategies is finite and
known at compile time.

## 5. Property-based Proxy for EF Core

**Classic pattern**: Proxy with method delegation.

**Granit variant**: `FilterProxy` exposes **properties** (not methods) because
EF Core can only translate property accesses in query filters.

```csharp
// src/Granit.Persistence/Extensions/ModelBuilderExtensions.cs
internal sealed class FilterProxy(IDataFilter? dataFilter, ICurrentTenant? tenant)
{
    public bool SoftDeleteEnabled => dataFilter?.IsEnabled<ISoftDeletable>() ?? true;
}
```

This works around an EF Core limitation, invisible to application developers.

## 6. Dual Sync/Async Template Method

**Classic pattern**: Template Method with abstract hooks.

**Granit variant**: `GranitModule` exposes sync/async pairs. The async version
delegates to the sync version by default.

```csharp
// A module can override one OR the other — not required to implement both
public virtual Task ConfigureServicesAsync(ServiceConfigurationContext context)
{
    ConfigureServices(context);
    return Task.CompletedTask;
}
```

Avoids forcing async on simple modules while allowing it for modules that need
it (e.g., Vault secret retrieval during startup).

## 7. Implicit Outbox via Handler Return Type

**Classic pattern**: Explicit transactional outbox.

**Granit variant** (native Wolverine): a handler returning `IEnumerable<T>`
automatically produces Outbox messages.

```csharp
public static IEnumerable<object> Handle(CreatePatientCommand cmd, AppDbContext db)
{
    db.Patients.Add(new Patient { /* ... */ });
    yield return new PatientCreatedOccurred { /* ... */ };  // local queue
    yield return new SendWelcomeEmailCommand { /* ... */ }; // Outbox
}
```

The handler is pure and declarative. Infrastructure (Outbox, routing) is
entirely managed by Wolverine.

## 8. Zero-allocation Streaming Hash

**Classic pattern**: SHA-256 on a complete buffer.

**Granit variant**: `IncrementalHash.CreateHash(SHA256)` +
`ArrayPool<byte>.Shared` in the idempotency middleware.

```text
src/Granit.Http.Idempotency/Internal/IdempotencyMiddleware.cs
```

The HTTP body is hashed in streaming mode without allocating a complete buffer,
critical for large requests (uploads).

## 9. Multi-tenant Composite Key (idempotency)

**Classic pattern**: Idempotency key = client header.

**Granit variant**: The Redis key includes `{tenantId}:{userId}:{key}`,
guaranteeing multi-tenant and multi-user isolation.

Two different tenants can use the same idempotency key without collision.
A single user with two clients cannot replay another client's request.

## 10. Expression Tree Query Filters

**Classic pattern**: Static EF Core query filters.

**Granit variant**: `ApplyGranitConventions()` dynamically registers one **named**
`HasQueryFilter` per applicable interface per entity (EF Core 10).

```text
src/Granit.Persistence/Extensions/ModelBuilderExtensions.cs
```

Each filter is independent — bypass one per query with
`IgnoreQueryFilters([GranitFilterNames.SoftDelete])` without affecting the others.
Before EF Core 10, `HasQueryFilter()` silently overwrote the previous filter,
requiring a combined `AndAlso` expression as a workaround. Named filters handle
`ISoftDeletable` + `IActive` + `IMultiTenant` + `IProcessingRestrictable` +
`IPublishable` independently.
