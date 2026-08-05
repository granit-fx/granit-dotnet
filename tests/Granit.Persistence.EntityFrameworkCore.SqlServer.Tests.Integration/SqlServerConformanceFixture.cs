using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;
using Granit.Testing.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Integration;

/// <summary>
/// Starts one SQL Server 2022 container for the whole conformance collection and implements
/// the provider contract for the abstract suites of <c>Granit.Testing.Persistence</c>.
/// </summary>
public sealed class SqlServerConformanceFixture : IRelationalConformanceFixture, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <inheritdoc/>
    public string ProviderName => "SQL Server";

    /// <inheritdoc/>
    public void UseProvider(DbContextOptionsBuilder builder) =>
        builder.UseSqlServer(_container.GetConnectionString());

    /// <inheritdoc/>
    /// <remarks>
    /// Resolves the lock through the real registration path (<c>AddGranitSqlServer()</c>) so
    /// the suite also covers the SqlClient factory registration — the exact gap that made
    /// this provider's lock a silent no-op before #3152. A fresh host per call yields an
    /// independent "replica".
    /// </remarks>
    public IGranitMigrationLock CreateMigrationLock()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString();
        builder.Services.AddLogging();
        builder.AddGranitSqlServer();
        return builder.Services.BuildServiceProvider().GetRequiredService<IGranitMigrationLock>();
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => new(_container.StartAsync());

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

/// <summary>Collection sharing the single SQL Server container across all conformance classes.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerConformanceSuite : ICollectionFixture<SqlServerConformanceFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "sqlserver-conformance";
}
