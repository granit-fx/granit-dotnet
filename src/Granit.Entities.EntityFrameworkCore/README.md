# Granit.Entities.EntityFrameworkCore

EF Core executor for the `Granit.Entities` calendar range endpoint
(`GET /api/entities/{name}/calendar`). Resolves `IQueryableSource<TEntity>` for
each registered `EntityDefinition` declaring a `CalendarLayoutDescriptor`,
composes the overlap filter built by `Granit.Entities.Abstractions`, projects
rows into `CalendarItemResponse` via reflection-built expressions, and
dispatches per entity name with **zero request-time reflection** — one
closed-generic `CalendarRangeRunner<T>` per entity, registered at composition
time through `AddGranitEntitiesEntityFrameworkCore()`.

Also wires automatic FusionCache invalidation on entity-lifecycle events for
entities that emit them (`IEmitEntityLifecycleEvents`): every cached calendar
window for the affected entity is dropped via the per-entity eviction tag.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.EntityFrameworkCore
```

## Dependencies

- `Granit.Entities.Endpoints`
- `Granit.Persistence.EntityFrameworkCore`
- `Granit.QueryEngine.Abstractions`

## Usage

```csharp
builder.Services
    .AddEntityDefinition<Meeting, MeetingEntityDefinition>()
    .AddEntityDefinition<Reservation, ReservationEntityDefinition>();

// MUST be called AFTER every AddEntityDefinition — the loop reads the
// registered descriptors at this moment to wire one closed-generic
// CalendarRangeRunner<T> per entity that exposes a CalendarLayoutDescriptor.
builder.Services.AddGranitEntitiesEntityFrameworkCore();
```

Without this package, the calendar endpoint stays mounted but returns an empty
list (the framework's `NullCalendarRangeService` default).

## License

Apache-2.0
