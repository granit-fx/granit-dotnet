using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Starts a PostgreSQL 17 container shared by the integration tests in this assembly.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
