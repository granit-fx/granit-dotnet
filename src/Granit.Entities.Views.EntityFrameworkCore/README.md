# Granit.Entities.Views.EntityFrameworkCore

EF Core persistence for the `EntityView` aggregate
([ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/)) — isolated
`EntityViewDbContext`, JSON state + audience persistence through value-converters,
and the `IEntityViewReader` / `IEntityViewWriter` implementations gated by the
closed permission set of `EntityViewPermissions` (§6).

The host wires its own `EntityViewDbContext` via the chosen provider
(PostgreSQL / SQL Server / SQLite) — this package does not bind a provider so
that consumers stay portable.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Views.EntityFrameworkCore
```

## Dependencies

- `Granit.Authorization`
- `Granit.Entities.Views`
- `Granit.Persistence.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
