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

## Duplicate detection support (PostgreSQL)

Tier-1 deterministic detection (canonical `CanonicalEmail`, `CanonicalNumber`,
`TaxId` columns) ships out of the box for every supported provider via the
`PartyCanonicalisationInterceptor`.

Tier-2 trigram blocking is **PostgreSQL-only**. The framework cannot ship
migrations directly, so the consuming app opts in by calling the helper from
its own migration:

```csharp
public partial class AddPartyTrigramIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddPartyTrigramSimilarityIndexes();

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.RemovePartyTrigramSimilarityIndexes();
}
```

The helper installs the `pg_trgm` extension and a GIST index on `lower(name)`
with `gist_trgm_ops`. SQL Server / SQLite / InMemory: the helper is a no-op and
Tier-2 falls back to a slower `LIKE`-based query at scan time.

Tunable thresholds via `appsettings.json`:

```json
{
  "Granit": {
    "Parties": {
      "Deduplication": {
        "NameSimilarityThreshold": 0.7,
        "CompanySimilarityThreshold": 0.6
      }
    }
  }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
