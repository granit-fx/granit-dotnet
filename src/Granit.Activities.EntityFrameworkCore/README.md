# Granit.Activities.EntityFrameworkCore

EF Core persistence for the [Granit.Activities](../Granit.Activities/README.md)
runtime — isolated `ActivitiesDbContext` with the framework's standard
audit / soft-delete / multi-tenant interceptors, plus the table configuration
and indexes that keep the assignee inbox and overdue-scan queries fast.

## Wire-up

```csharp
// In the host's Program.cs
builder.Services
    .AddGranitActivities()                           // registry + standard catalog
    .AddGranitActivitiesEntityFrameworkCore(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

`AddGranitActivitiesEntityFrameworkCore` registers `ActivitiesDbContext` via
`AddGranitDbContext`, which automatically wires:

- `AuditedEntityInterceptor` — populates `CreatedAt` / `CreatedBy` / `LastModifiedAt` / `LastModifiedBy`
- `SoftDeleteInterceptor` — translates `Remove(...)` into `IsDeleted = true`
- `EntityLifecycleEventInterceptor` — emits `EntityCreated/Updated/DeletedEvent<Activity>` on `SavedChanges`; hosts emit `EntityBulkUpdatedEvent<Activity>` themselves for bulk operations
- The standard tenant filter + soft-delete filter via `ApplyGranitConventions`

## Schema

| Column | Type | Notes |
| ------ | ---- | ----- |
| `Id` | `uuid` | PK |
| `EntityType` | `varchar(256)` | Polymorphic FK target — the host entity wire identifier |
| `EntityId` | `uuid` | Polymorphic FK target — host row id |
| `Type` | `varchar(64)` | Activity type name — must resolve in `IActivityRegistry` |
| `AssignedToUserId` | `uuid` | |
| `CreatedByUserId` | `uuid?` | |
| `DueAt` | `timestamptz` | |
| `Description` | `varchar(2000)?` | |
| `Status` | `int` | `Open=0` / `Done=1` / `Cancelled=2` (`Overdue` is computed, never persisted) |
| `CompletedAt` / `CompletedByUserId` | `timestamptz?` / `uuid?` | Set on terminal transition |
| `OverdueNotifiedAt` | `timestamptz?` | Idempotency guard for the overdue BG job (story A8) |
| `TenantId` | `uuid?` | Auto-populated by tenant filter |
| `IsDeleted` / `DeletedAt` / `DeletedBy` | from `FullAuditedAggregateRoot` | |
| `CreatedAt` / `CreatedBy` / `ModifiedAt` / `ModifiedBy` | from `FullAuditedAggregateRoot` | |

## Indexes

- `(EntityType, EntityId, TenantId, DueAt)` — per-entity activities panel
- `(TenantId, AssignedToUserId, Status, DueAt)` — "my open activities" inbox
- `(TenantId, Status, DueAt)` — overdue background scan (story A8)

## Migrations

Per Granit convention, **migrations are not shipped with framework packages**.
The consuming application generates and ships its own migrations against the
host's DbContext. Reference table layout is documented above; the configuration
honours `GranitActivitiesDbProperties.DbTablePrefix` and
`GranitActivitiesDbProperties.DbSchema` for hosts that need to override.
