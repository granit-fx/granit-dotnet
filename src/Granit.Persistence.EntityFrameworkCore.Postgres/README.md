# Granit.Persistence.EntityFrameworkCore.Postgres

PostgreSQL-specific persistence extensions for Granit applications.

## Features

- **`NpgsqlAdvisoryMigrationLock`** — distributed migration lock using PostgreSQL advisory locks (`pg_advisory_lock`)
- **Zero Npgsql NuGet dependency** — discovers the provider at runtime via `DbProviderFactories`

## Quick start

```csharp
// In Program.cs or host module — order relative to AddGranitMigrateSupport() does not
// matter for the migration lock (the real lock always replaces the NullMigrationLock
// fallback). Only tenant-per-schema setups still need AddGranitPostgres() before
// AddTenantPerSchemaDbContext() so the schema activator is not overridden by a no-op.
builder.AddGranitPostgres();
builder.AddGranitMigrateSupport();
```

Or via module dependency:

```csharp
using Granit.Persistence.EntityFrameworkCore.Postgres;

[DependsOn(typeof(GranitPersistenceEntityFrameworkCorePostgresModule))]
public sealed class AppHostModule : GranitModule { }
```
