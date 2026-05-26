using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL 17 container shared across the indexing integration suite. Boots once,
/// per-test schemas avoid cross-pollution.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
