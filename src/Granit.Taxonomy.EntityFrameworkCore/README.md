# Granit.Taxonomy.EntityFrameworkCore

EF Core persistence layer for `Granit.Taxonomy`. Provides the isolated
`TaxonomyDbContext`, configurable table prefix and schema
(`GranitTaxonomyDbProperties`), and `ModelBuilder` extensions for embedding the
module into a host-owned `DbContext`. Compatible with SQL Server and PostgreSQL.

Ships the `Tag` table with the `(TenantId, Scope, Name)` uniqueness invariant
locked by ADR-054 and an internal `TagService : ITagService` implementing the
CRUD surface.

Migrations are not shipped from the framework package — the consuming
application generates them against its own composed model.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Taxonomy.EntityFrameworkCore
```

## Dependencies

- `Granit.Taxonomy`
- `Granit.Persistence.EntityFrameworkCore`

## Documentation

See [ADR-054](../../docs-site/src/content/docs/dotnet/architecture/adr/054-taxonomy-module.md)
for the architecture decisions and the [full documentation](https://granit-fx.dev).
