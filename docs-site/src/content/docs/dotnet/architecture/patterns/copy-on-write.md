---
title: "Copy-on-Write"
description: "How Granit uses immutable state with ImmutableDictionary to ensure thread-safe data filtering"
sidebar:
  order: 45
---

## Definition

The Copy-on-Write pattern guarantees thread-safety by creating a new copy of
the data structure on each modification, instead of mutating the existing one.
The previous state remains intact, enabling simple restoration and eliminating
data races.

## Diagram

```mermaid
sequenceDiagram
    participant A as Async flow A
    participant AL as AsyncLocal
    participant B as Async flow B (child)

    A->>AL: State = {SoftDelete: true, Active: true}
    A->>B: Creates a child flow
    B->>B: Inherits parent state

    B->>AL: Disable ISoftDeletable
    AL->>AL: New dict = {SoftDelete: false, Active: true}
    Note over AL: The original is not modified

    A->>AL: Reads state
    AL-->>A: {SoftDelete: true, Active: true}
    Note over A: Not impacted by B's modification
```

## Implementation in Granit

| Component | File | Structure |
|-----------|------|-----------|
| `DataFilter` | `src/Granit.Core/DataFiltering/DataFilter.cs` | `AsyncLocal<ImmutableDictionary<Type, bool>>` |

The `DataFilter` uses `ImmutableDictionary<Type, bool>.SetItem()` which returns
a **new** dictionary without mutating the original. Combined with
`AsyncLocal<T>`, this guarantees:

1. **Flow isolation**: a child flow that modifies filters does not disturb the
   parent flow
2. **Restoration**: the `IDisposable` scope keeps a reference to the old
   dictionary and restores it on `Dispose()`
3. **No locks**: `ImmutableDictionary` is inherently thread-safe

### Pitfall avoided -- child flow mutation

Without copy-on-write, an `AsyncLocal<Dictionary<T>>` shared between parent
and child could see the child's mutations affect the parent. With
`ImmutableDictionary`, `SetItem()` creates a new reference that does not
propagate back to the parent.

## Rationale

Data filtering must be isolated per request scope. If a middleware temporarily
disables soft delete for an admin operation, that change must not leak into
other concurrent requests or child `Task.Run()` flows.

## Usage example

```csharp
// Copy-on-write is transparent -- the API remains simple
using (dataFilter.Disable<ISoftDeletable>())
{
    // New ImmutableDictionary created: {SoftDelete: false}
    // The original remains intact

    await Task.Run(async () =>
    {
        // This child flow inherits {SoftDelete: false}
        // But if the child calls Enable<ISoftDeletable>(),
        // it creates a NEW dict for the child without touching the parent
    });
}
// Dispose() restores the original ImmutableDictionary
```
