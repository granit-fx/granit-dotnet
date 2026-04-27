# Granit.Parties.EntityFrameworkCore

EF Core persistence for [Granit.Parties](../Granit.Parties/README.md). Provides
the isolated `PartiesDbContext`, entity configurations for `Party` and its
multi-valued children (`PartyAddress`, `PartyEmail`, `PartyPhone`,
`PartyExternalMapping`), and the `EfPartyStore` reader/writer pair used by
the aggregate's reconciliation logic.

Implements `Granit.Persistence` conventions: `IMultiTenant` query filter, soft
delete, audit interceptors, and `Granit.QueryEngine` translator support for the
admin grid.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.EntityFrameworkCore
```

## Dependencies

- `Granit.Parties`
- `Granit.Persistence.EntityFrameworkCore`
- `Granit.QueryEngine.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
