# Granit.Persistence.Postgres

PostgreSQL-specific persistence extensions for Granit applications.

## Features

- **`NpgsqlAdvisoryMigrationLock`** — distributed migration lock using PostgreSQL advisory locks (`pg_advisory_lock`)
- **Zero Npgsql NuGet dependency** — discovers the provider at runtime via `DbProviderFactories`

## Quick start

```csharp
// In Program.cs or host module — call before AddGranitMigrateSupport()
builder.AddGranitPostgres();
builder.AddGranitMigrateSupport();
```

Or via module dependency:

```csharp
[DependsOn(typeof(GranitPersistencePostgresModule))]
public sealed class AppHostModule : GranitModule { }
```
