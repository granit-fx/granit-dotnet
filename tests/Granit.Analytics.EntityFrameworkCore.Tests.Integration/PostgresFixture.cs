using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Boots a PostgreSQL 17 container once per test class so we can verify that the
/// empty-set semantics promised by <c>MetricExecutor</c> on Sqlite (the unit-test
/// provider) hold byte-for-byte on the production database engine. Provider parity is
/// the central security claim of A3 — without it, Sqlite/Postgres divergence on
/// SUM(NULL) or AVG(empty) could let the contract drift unnoticed.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
