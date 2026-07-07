# Granit.Wolverine.SqlServer

SQL Server transport for Granit.Wolverine using Wolverine.SqlServer.
Provides durable outbox, EF Core transaction integration, and per-tenant database routing.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Wolverine.SqlServer
```

## Dependencies

- `Granit.Persistence`
- `Granit.Wolverine`

## Configuration

`AddGranitWolverineWithSqlServer()` binds the `Wolverine:SqlServer` section and
validates it on startup. At least **one** connection-string source must be set, or
startup fails with `OptionsValidationException`:

```json
{
  "Wolverine": {
    "SqlServer": {
      "TransportConnectionStringName": "DefaultConnection"
    }
  }
}
```

| Key | Notes |
| --- | ----- |
| `TransportConnectionString` | Explicit connection string. Takes priority over the name. |
| `TransportConnectionStringName` | Key into `ConnectionStrings` (Aspire-friendly). |
| `SchemaName` | Optional. Defaults to the Granit host schema (`GranitDbDefaults.HostDbSchema`), then Wolverine's built-in default schema. Set only to override the envelope-table schema. |

Envelope tables are created by the `--migrate` pipeline (`IExternalStoreMigrator`),
never at application startup — the runtime database user does not need DDL grants.

`AddGranitWolverine()` must run before `AddGranitWolverineWithSqlServer()` —
`GranitWolverineSqlServerModule` handles this ordering automatically via `[DependsOn]`.

## Documentation

See the [full documentation](https://granit-fx.dev).
