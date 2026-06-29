# Granit.Wolverine.Postgresql

PostgreSQL transport for Granit.Wolverine using Wolverine.Postgresql.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Wolverine.Postgresql
```

## Dependencies

- `Granit.Persistence`
- `Granit.Wolverine`

## Configuration

`AddGranitWolverineWithPostgresql()` binds the `Wolverine:Postgresql` section and
validates it on startup. At least **one** connection-string source must be set, or
startup fails with `OptionsValidationException`:

```json
{
  "Wolverine": {
    "Postgresql": {
      "TransportConnectionStringName": "DefaultConnection"
    }
  }
}
```

| Key | Notes |
| --- | ----- |
| `TransportConnectionString` | Explicit connection string. Takes priority over the name. |
| `TransportConnectionStringName` | Key into `ConnectionStrings` (Aspire-friendly). |
| `SchemaName` | Optional. Defaults to the Granit host schema (`GranitDbDefaults.HostDbSchema`), then Wolverine's built-in `wolverine` schema. Set only to override the envelope-table schema. |

`AddGranitWolverine()` must run before `AddGranitWolverineWithPostgresql()` —
`GranitWolverinePostgresqlModule` handles this ordering automatically via `[DependsOn]`.

## Documentation

See the [full documentation](https://granit-fx.dev).
