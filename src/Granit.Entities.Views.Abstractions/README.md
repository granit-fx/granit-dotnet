# Granit.Entities.Views.Abstractions

Lightweight contracts for the Granit `EntityView` primitive — saved views over
compiled collections, with Personal / Shared / Tenant visibility and three
promotion flags (pinned / default / personal-default). See
[ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/).

Reference this package from any module that surfaces a saved-view UI (e.g. the
manifest endpoints). Reference `Granit.Entities.Views` only from hosts that
resolve the aggregate.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Views.Abstractions
```

## Dependencies

- `Granit`
- `Granit.Entities.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev) and
[ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/).
