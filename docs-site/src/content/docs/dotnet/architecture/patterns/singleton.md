---
title: "Singleton Pattern — DI-Managed Instances"
description: "Manage shared state safely in .NET — DI-registered singletons for stateless services, AsyncLocal for per-request tenant context and data filter state without thread leaks."
sidebar:
  label: Singleton
  order: 30
---

## Definition

The Singleton pattern ensures a class has only one instance and provides a
global access point to it. In Granit, the Singleton takes two forms: DI
singletons (managed by the container) and `AsyncLocal` singletons
(thread-safe state per async flow).

## Diagram

```mermaid
classDiagram
    class NullTenantContext {
        -Instance : NullTenantContext
        +IsAvailable : bool = false
        +Id : Guid? = null
    }

    class CurrentTenant {
        -_current : AsyncLocal of TenantInfo
        +Id : Guid?
        +IsAvailable : bool
        +Change(tenantId) IDisposable
    }

    class DataFilter {
        -_state : AsyncLocal of ImmutableDictionary
        +IsEnabled of T() bool
        +Disable of T() IDisposable
    }
```

## Implementation in Granit

| Singleton | File | Type | Scope |
|-----------|------|------|-------|
| `NullTenantContext.Instance` | `src/Granit.Core/MultiTenancy/NullTenantContext.cs` | `static readonly` | Global |
| `CurrentTenant._current` | `src/Granit.MultiTenancy/CurrentTenant.cs` | `AsyncLocal<TenantInfo?>` | Per async flow |
| `DataFilter._state` | `src/Granit.Core/DataFiltering/DataFilter.cs` | `AsyncLocal<ImmutableDictionary>` | Per async flow |
| DI services | All modules | `AddSingleton<T>()` | DI container |

**Custom variant -- AsyncLocal Singleton**: a `static readonly AsyncLocal<T>`
field provides singleton state per `async/await` flow, thread-safe without
locks. Each `Task` inherits state from its parent, but modifications are
isolated per flow thanks to copy-on-write (`ImmutableDictionary`).

## Rationale

`NullTenantContext` eliminates `null` checks throughout the framework when
multi-tenancy is not installed. `AsyncLocal` singletons allow propagating
context (tenant, filters) across `async/await` boundaries without relying on
`HttpContext`.

## Usage example

```csharp
// NullTenantContext -- always available, never null
ICurrentTenant tenant = serviceProvider.GetRequiredService<ICurrentTenant>();
// If Granit.MultiTenancy is not installed: tenant.IsAvailable == false
// No NullReferenceException, no if (tenant != null) check

// AsyncLocal -- state isolated per async flow
using (currentTenant.Change(newTenantId))
{
    // This async flow sees newTenantId
    await Task.Run(async () =>
    {
        // This child flow inherits newTenantId
        Guid? id = currentTenant.Id; // == newTenantId
    });
}
// Here, the previous tenant is restored
```

## Further reading

- [Singleton -- refactoring.guru](https://refactoring.guru/design-patterns/singleton)
