using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Boots one PostgreSQL 17 container per test class — the OData filter
/// composition is checked against the production database engine, since
/// SQLite would not catch differences in <c>WHERE</c> clause translation.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
