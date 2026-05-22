# Granit.Presence.EntityFrameworkCore

EF Core persistence layer for `Granit.Presence`. Stores the manual presence override
(status + optional expiration) in PostgreSQL. The live heartbeat is NOT persisted — it
lives in FusionCache (L1 + optional Redis L2 backplane).

## Registration

```csharp
builder.AddGranitPresence();
builder.AddGranitPresenceEntityFrameworkCore(opts => opts.UseNpgsql(connectionString));
```

The dedicated `PresenceDbContext` inherits `GranitDbContext` for the canonical conventions.
EF Core migrations are owned by the consuming application — the framework does NOT ship a
migration.
