# Granit.Contacts.EntityFrameworkCore

EF Core persistence for [Granit.Contacts](../Granit.Contacts/README.md). Provides
the isolated `ContactsDbContext`, entity configurations for `Contact` and its
multi-valued children (`ContactAddress`, `ContactEmail`, `ContactPhone`,
`ContactExternalMapping`), and the `EfContactStore` reader/writer pair used by
the aggregate's reconciliation logic.

Implements `Granit.Persistence` conventions: `IMultiTenant` query filter, soft
delete, audit interceptors, and `Granit.QueryEngine` translator support for the
admin grid.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts.EntityFrameworkCore
```

## Dependencies

- `Granit.Contacts`
- `Granit.Persistence.EntityFrameworkCore`
- `Granit.QueryEngine.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
