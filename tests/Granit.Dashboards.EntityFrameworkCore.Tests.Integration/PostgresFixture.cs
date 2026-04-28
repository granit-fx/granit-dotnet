using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Boots a PostgreSQL 17 container once per test class so we can verify the
/// production schema shape, multi-tenant isolation, and soft-delete behaviour
/// against the real database engine. SQLite (used in the unit-test project) does
/// not enforce some constraints / indexes the way Postgres does — provider parity
/// is the central security claim of B2.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
