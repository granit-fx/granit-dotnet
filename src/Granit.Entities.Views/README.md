# Granit.Entities.Views

Runtime domain for the Granit `EntityView` primitive — saved views over compiled
collections, with Personal / Shared / Tenant visibility and three promotion flags
(pinned / default / personal-default). See
[ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/).

This package ships the aggregate + value objects + validation. Persistence lives
in `Granit.Entities.Views.EntityFrameworkCore` (sibling); HTTP endpoints live in
`Granit.Entities.Views.Endpoints` (sibling).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Views
```

## Dependencies

- `Granit.Entities.Views.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev) and
[ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/).
