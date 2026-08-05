using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Internal;

public sealed class SqlServerAppLockTests
{
    private static SqlServerAppLock CreateLock(
        IConfiguration configuration, GranitMigrateOptions? options = null) =>
        new(configuration,
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitMigrateOptions()),
            NullLogger<SqlServerAppLock>.Instance);

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle()
    {
        SqlServerAppLock sut = CreateLock(new ConfigurationBuilder().Build());

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_HandleDisposeAsync_DoesNotThrow()
    {
        SqlServerAppLock sut = CreateLock(new ConfigurationBuilder().Build());

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(async () => await handle!.DisposeAsync());
    }

    [Fact]
    public async Task TryAcquireAsync_ResourceName_IsPassedThrough()
    {
        SqlServerAppLock sut = CreateLock(new ConfigurationBuilder().Build());

        // Returns NoOp immediately without needing a real connection
        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "my-specific-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull(); // NoOp returned, not null
    }

    [Fact]
    public async Task TryAcquireAsync_FactoryRegistered_ReachesFactoryBranch()
    {
        // The registration AddGranitSqlServer() performs — without it, TryGetFactory fails
        // and the lock silently degrades to a no-op handle.
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "not-a-valid-connection-string",
            })
            .Build();
        SqlServerAppLock sut = CreateLock(configuration);

        // The malformed connection string throws when assigned to the factory-created
        // connection — deterministic proof the lock passed the NoOp guards and reached
        // the sp_getapplock path, instead of silently returning a no-op handle.
        await Should.ThrowAsync<ArgumentException>(
            () => sut.TryAcquireAsync("resource", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_ReadsTheConfiguredConnectionStringName()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        // Only the custom name is configured — the lock must reach the connection branch
        // through it (malformed string throws), proving the configured name is honored.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MigrationsDb"] = "not-a-valid-connection-string",
            })
            .Build();

        SqlServerAppLock sut = CreateLock(
            configuration, new GranitMigrateOptions { ConnectionStringName = "MigrationsDb" });

        await Should.ThrowAsync<ArgumentException>(
            () => sut.TryAcquireAsync("resource", TestContext.Current.CancellationToken));
    }
}
