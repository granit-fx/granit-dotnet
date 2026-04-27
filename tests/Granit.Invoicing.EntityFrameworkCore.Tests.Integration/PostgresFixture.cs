using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Invoicing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Starts a PostgreSQL 17 container shared by the integration tests in this assembly.
/// Required for tests that exercise <c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>
/// (unsupported by the EF in-memory provider).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
