# Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Integration

SQL Server conformance run of the `Granit.Testing.Persistence` suites against a real
`mssql/server:2022` Testcontainer: tenant isolation, query-filter SQL, interceptor
pipeline (incl. real `DbUpdateConcurrencyException`), enum-as-string columns, and
migration-lock contention (`sp_getapplock`) through the real `AddGranitSqlServer()`
registration path — including the SqlClient factory registration whose absence made
the lock a silent no-op before #3152.

## Known provider parity gaps

Schema-per-tenant isolation is PostgreSQL-only by design (`SET search_path`); the
conformance suites do not cover it, so no skip is needed here. Any future suite a
provider genuinely cannot support must be an explicit documented skip, never a
silently absent test class.

## Running locally

Requires a reachable Docker daemon. On WSL:

```bash
DOCKER_HOST=tcp://localhost:2375 dotnet test tests/Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Integration
```

CI: `integration` shard. Unit-test jobs exclude this project via
`-p:SkipIntegrationTests=true` (`IsTestProject` flips to `false`).
