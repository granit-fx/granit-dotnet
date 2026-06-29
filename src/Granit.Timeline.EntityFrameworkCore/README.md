# Granit.Timeline.EntityFrameworkCore

EF Core persistence for Granit.Timeline. Provides PostgreSQL-backed `EfCoreTimelineStore`
and `EfCoreTimelineQuery` with ISO 27001-compliant INSERT-only audit trail for system logs.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Timeline.EntityFrameworkCore
```

## Dependencies

- `Granit.Persistence`
- `Granit.Timeline`

## Usage

EF Core persistence is opt-in: without it, `Granit.Timeline` keeps its in-memory
stores. After `AddGranitTimeline()`, wire the EF Core store in your host module:

```csharp
builder.AddGranitTimelineEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

This replaces `InMemoryTimelineStore` / `InMemoryTimelineQuery` with
`EfCoreTimelineStore` / `EfCoreTimelineQuery` and registers the internal
`TimelineDbContext` via `IDbContextFactory`. The module manages that
`TimelineDbContext` itself (it already calls `ConfigureTimelineModule`
internally) — consumers do **not** need to add `ConfigureTimelineModule()` to
their own `DbContext`.

## Documentation

See the [full documentation](https://granit-fx.dev).
