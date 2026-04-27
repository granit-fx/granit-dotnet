using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Starts a PostgreSQL 17 container once per test class and exposes the connection string.
/// Required because the rewriters use <c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>,
/// which are not supported by the EF in-memory provider.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
