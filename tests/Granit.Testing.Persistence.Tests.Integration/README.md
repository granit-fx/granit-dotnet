# Granit.Testing.Persistence.Tests.Integration

PostgreSQL conformance run of the `Granit.Testing.Persistence` suites against a real
`postgres:17-alpine` Testcontainer: tenant isolation, query-filter SQL, interceptor
pipeline (incl. real `DbUpdateConcurrencyException`), enum-as-string columns, and
migration-lock contention through the real `AddGranitPostgres()` registration path.

## Running locally

Requires a reachable Docker daemon. On WSL:

```bash
DOCKER_HOST=tcp://localhost:2375 dotnet test tests/Granit.Testing.Persistence.Tests.Integration
```

CI: `integration` shard. Unit-test jobs exclude this project via
`-p:SkipIntegrationTests=true` (`IsTestProject` flips to `false`).
