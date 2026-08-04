using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace Granit.Testing.Persistence.Tests.Integration;

/// <summary>
/// Starts one PostgreSQL 17 container for the whole conformance collection and implements
/// the provider contract for the abstract suites.
/// </summary>
public sealed class PostgresConformanceFixture : IRelationalConformanceFixture, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    /// <inheritdoc/>
    public string ProviderName => "PostgreSQL";

    /// <inheritdoc/>
    public void UseProvider(DbContextOptionsBuilder builder) =>
        builder.UseNpgsql(_container.GetConnectionString());

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves the lock through the real registration path (<c>AddGranitPostgres()</c>) so the
    /// suite also covers factory registration — the gap that made the SQL Server lock a silent
    /// no-op (#3152). A fresh host per call yields an independent "replica".
    /// </remarks>
    public IGranitMigrationLock CreateMigrationLock()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString();
        builder.Services.AddLogging();
        builder.AddGranitPostgres();
        return builder.Services.BuildServiceProvider().GetRequiredService<IGranitMigrationLock>();
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => new(_container.StartAsync());

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

/// <summary>Collection sharing the single PostgreSQL container across all conformance classes.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresConformanceSuite : ICollectionFixture<PostgresConformanceFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "postgres-conformance";
}
