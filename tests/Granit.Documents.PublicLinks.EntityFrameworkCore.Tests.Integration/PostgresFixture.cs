using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Tests.Integration;

/// <summary>Starts a PostgreSQL 17 container once per test collection and exposes the connection string.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
