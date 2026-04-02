# Granit.DataExchange.EntityFrameworkCore

EF Core persistence layer for `Granit.DataExchange`. Provides an isolated
`DataExchangeDbContext` with both import and export stores.

**Import**: `EfMappingStore`, `EfImportJobStore`, EF-backed identity resolvers
(`BusinessKeyResolver`, `CompositeKeyResolver`, `ExternalIdResolver`),
and batched `EfImportExecutor`.

**Export**: `EfExportPresetStore`, `EfExportJobStore`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.EntityFrameworkCore
```

## Dependencies

- `Granit.DataExchange`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
