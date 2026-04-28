# Granit.Dashboards.EntityFrameworkCore

EF Core persistence layer for Granit Dashboards. Ships the isolated
`DashboardsDbContext`, the EF configurations for the `Dashboard` aggregate and
its owned `WidgetInstance` rows, and the initial PostgreSQL schema migration.

Apply the standard Granit conventions: multi-tenant filter, soft-delete,
audited interceptor wiring — all via `ApplyGranitConventions` and
`AddGranitDbContext`.

Reference [Granit.Dashboards](../Granit.Dashboards/) for the runtime / registry
and [Granit.Dashboards.Abstractions](../Granit.Dashboards.Abstractions/) for the
declarative `DashboardDefinition` contracts.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards.EntityFrameworkCore
```

```csharp
builder.AddGranitDashboardsEntityFrameworkCore(opt => opt.UseNpgsql(connectionString));
```

## Dependencies

- `Granit.Dashboards`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
