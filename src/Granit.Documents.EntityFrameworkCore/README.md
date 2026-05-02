# Granit.Documents.EntityFrameworkCore

EF Core persistence layer for `Granit.Documents`. Provides the isolated `DocumentsDbContext`,
configurable table prefix and schema (`GranitDocumentsDbProperties`), and `ModelBuilder`
extensions for embedding the module into a host-owned `DbContext`. Compatible with SQL Server
and PostgreSQL.

Migrations are not shipped from the framework package — the consuming application generates
them against its own composed model.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.EntityFrameworkCore
```

## Dependencies

- `Granit.Documents`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See [ADR-052](../../docs-site/src/content/docs/dotnet/architecture/adr/052-documents-module.md)
for the architecture decisions and the [full documentation](https://granit-fx.dev).
