using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Internal;

public sealed class SqlServerAppLockTests
{
    private static SqlServerAppLock CreateLock(
        IConfiguration configuration,
        GranitMigrateOptions? options = null,
        IHostEnvironment? environment = null) =>
        new(configuration,
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitMigrateOptions()),
            NullLogger<SqlServerAppLock>.Instance,
            environment);

    // -------------------------------------------------------------------------
    // Fail-closed (default outside Development)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_Throws_ByDefault()
    {
        // No environment info = production assumption: missing prerequisites abort the
        // migration instead of silently proceeding unlocked (the pre-#3168 fail-open).
        SqlServerAppLock sut = CreateLock(new ConfigurationBuilder().Build());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.TryAcquireAsync("test-resource", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("RequireDistributedLock");
    }

    // -------------------------------------------------------------------------
    // Opt-out paths (Development, or explicit RequireDistributedLock = false)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle_InDevelopment()
    {
        SqlServerAppLock sut = CreateLock(
            new ConfigurationBuilder().Build(),
            environment: new FakeEnvironment(Environments.Development));

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
        await Should.NotThrowAsync(async () => await handle.DisposeAsync());
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle_WhenNotRequired()
    {
        SqlServerAppLock sut = CreateLock(
            new ConfigurationBuilder().Build(),
            new GranitMigrateOptions { RequireDistributedLock = false });

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "my-specific-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull(); // NoOp returned, not null
    }

    // -------------------------------------------------------------------------
    // Prerequisites present — the lock reaches the sp_getapplock path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_FactoryRegistered_ReachesFactoryBranch()
    {
        // The registration AddGranitSqlServer() performs — without it, TryGetFactory fails
        // and the lock (fail-closed) refuses to run.
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "not-a-valid-connection-string",
            })
            .Build();
        SqlServerAppLock sut = CreateLock(configuration);

        // The malformed connection string throws when assigned to the factory-created
        // connection — deterministic proof the lock passed the prerequisite guards and
        // reached the sp_getapplock path.
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

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
