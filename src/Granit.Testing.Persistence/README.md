# Granit.Testing.Persistence

Provider conformance kit for Granit persistence. Ships **abstract xUnit suites** that every
relational provider integration project inherits and runs against a **real database**
(Testcontainers) — the class of behavior the InMemory provider silently fakes.

## Suites

| Suite | Proves |
| ----- | ------ |
| `TenantIsolationConformanceSuite` | Tenant A never reads tenant B; absent tenant context fails closed to the host partition; the tenant filter is parameterised SQL (`@ef_filter__*`), never an inlined constant |
| `QueryFilterSqlConformanceSuite` | Soft-deleted rows survive physically but are filtered; `IActive` filter + `IDataFilter` bypass |
| `InterceptorPipelineConformanceSuite` | Audit stamping, concurrency-stamp rotation, real `DbUpdateConcurrencyException` on stale stamp, cross-tenant insert guard |
| `EnumPersistenceConformanceSuite` | Enum columns store the PascalCase value name (raw ADO read), roundtrip |
| `MigrationLockConformanceSuite` | Two "replica" locks contend on one resource — exactly one acquires; release frees; distinct resources don't contend |

## Usage (provider integration project)

```csharp
public sealed class PostgresConformanceFixture : IRelationalConformanceFixture, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public string ProviderName => "PostgreSQL";
    public void UseProvider(DbContextOptionsBuilder builder) => builder.UseNpgsql(_container.GetConnectionString());
    public IGranitMigrationLock CreateMigrationLock() { /* resolve via AddGranitPostgres() */ }
    // IAsyncLifetime: StartAsync / DisposeAsync
}

[CollectionDefinition(Name)]
public sealed class PostgresConformanceSuite : ICollectionFixture<PostgresConformanceFixture>
{
    public const string Name = "postgres-conformance";
}

[Collection(PostgresConformanceSuite.Name)]
public sealed class PostgresTenantIsolationTests(PostgresConformanceFixture fixture)
    : TenantIsolationConformanceSuite(fixture);
```

`CreateMigrationLock()` must resolve through the provider package's **real DI registration
path** (`AddGranitPostgres()` / `AddGranitSqlServer()`) so the suite also covers factory
registration — the gap that made the SQL Server lock a silent no-op (#3152).

A provider capability genuinely unsupported (e.g. schema-per-tenant on SQL Server) is an
explicit documented skip in the provider project — never a silently absent test class.

## Reference implementation

`tests/Granit.Testing.Persistence.Tests.Integration` (PostgreSQL). Local run on WSL:

```bash
DOCKER_HOST=tcp://localhost:2375 dotnet test tests/Granit.Testing.Persistence.Tests.Integration
```

CI: runs in the `integration` shard; excluded from unit-test jobs via
`-p:SkipIntegrationTests=true`.
