# Granit.Persistence.SqlServer

SQL Server-specific persistence extensions for Granit applications.

## Features

- **`SqlServerAppLock`** — distributed migration lock using SQL Server application locks (`sp_getapplock`)
- **Zero SqlClient NuGet dependency** — discovers the provider at runtime via `DbProviderFactories`

## Quick start

```csharp
// In Program.cs or host module — call before AddGranitMigrateSupport()
builder.AddGranitSqlServer();
builder.AddGranitMigrateSupport();
```

Or via module dependency:

```csharp
[DependsOn(typeof(GranitPersistenceSqlServerModule))]
public sealed class AppHostModule : GranitModule { }
```
