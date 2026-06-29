# Granit.Timeline

Unified activity stream engine for Granit entities.
Aggregates comments, internal notes, and system logs per entity via `ITimelined`
marker interface. Provides `ITimelineStore`, `ITimelineQuery`,
`ITimelineFollowerService`, and `ITimelineNotifier` abstractions.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Timeline
```

## Dependencies

- `Granit`
- `Granit.Guids`
- `Granit.QueryEngine`
- `Granit.Users`
- `Granit.Timing`

## Persistence

By default `Granit.Timeline` registers **in-memory** stores, suitable for
development and tests only — data is lost on restart. For durable persistence,
add the `Granit.Timeline.EntityFrameworkCore` package and wire it in your
host/infrastructure setup:

```csharp
builder.AddGranitTimelineEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

This replaces the in-memory stores with EF Core-backed ones. The package ships a
bundled `TimelineDbContext` that already maps the schema; only if you fold the
Timeline model into your own DbContext do you call
`modelBuilder.ConfigureTimelineModule()` in `OnModelCreating`.

## Documentation

See the [full documentation](https://granit-fx.dev).
