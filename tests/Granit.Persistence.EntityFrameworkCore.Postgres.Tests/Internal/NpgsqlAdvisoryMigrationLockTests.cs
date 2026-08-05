using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Internal;

public sealed class NpgsqlAdvisoryMigrationLockTests
{
    private static NpgsqlAdvisoryMigrationLock CreateLock(
        IConfiguration configuration,
        GranitMigrateOptions? options = null,
        IHostEnvironment? environment = null) =>
        new(configuration,
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitMigrateOptions()),
            NullLogger<NpgsqlAdvisoryMigrationLock>.Instance,
            environment);

    // -------------------------------------------------------------------------
    // Fail-closed (default outside Development)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_Throws_ByDefault()
    {
        // No environment info = production assumption: missing prerequisites abort the
        // migration instead of silently proceeding unlocked (the pre-#3168 fail-open).
        NpgsqlAdvisoryMigrationLock sut = CreateLock(new ConfigurationBuilder().Build());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.TryAcquireAsync("test-resource", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("RequireDistributedLock");
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_Throws_InDevelopment_WhenExplicitlyRequired()
    {
        NpgsqlAdvisoryMigrationLock sut = CreateLock(
            new ConfigurationBuilder().Build(),
            new GranitMigrateOptions { RequireDistributedLock = true },
            new FakeEnvironment(Environments.Development));

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.TryAcquireAsync("test-resource", TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Opt-out paths (Development, or explicit RequireDistributedLock = false)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle_InDevelopment()
    {
        NpgsqlAdvisoryMigrationLock sut = CreateLock(
            new ConfigurationBuilder().Build(),
            environment: new FakeEnvironment(Environments.Development));

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
        await handle.DisposeAsync(); // NoOp — should not throw
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle_WhenNotRequired()
    {
        NpgsqlAdvisoryMigrationLock sut = CreateLock(
            new ConfigurationBuilder().Build(),
            new GranitMigrateOptions { RequireDistributedLock = false });

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
        await Should.NotThrowAsync(async () => await handle.DisposeAsync());
    }

    [Fact]
    public async Task TryAcquireAsync_ReadsTheConfiguredConnectionStringName()
    {
        // Only "DefaultConnection" is configured, but the options point the lock at
        // "MigrationsDb" — the lock must look up the configured NAME, not the default,
        // so it lands on the no-connection-string branch (NoOp handle, not required).
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=somewhere;Database=db",
            })
            .Build();

        NpgsqlAdvisoryMigrationLock sut = CreateLock(
            configuration,
            new GranitMigrateOptions
            {
                ConnectionStringName = "MigrationsDb",
                RequireDistributedLock = false,
            });

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull(); // NoOp — "MigrationsDb" is absent from configuration
    }

    // -------------------------------------------------------------------------
    // Stable bigint advisory key
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeAdvisoryKey_IsStable()
    {
        // Contractual: the key must be deterministic across processes and versions.
        // A drift here changes the lock key mid-fleet and breaks the rolling-upgrade
        // guarantee — change ONLY with a dual-acquire transition plan (see class remarks).
        long key = NpgsqlAdvisoryMigrationLock.ComputeAdvisoryKey("GranitMigration");

        key.ShouldBe(NpgsqlAdvisoryMigrationLock.ComputeAdvisoryKey("GranitMigration"));
        key.ShouldNotBe(NpgsqlAdvisoryMigrationLock.ComputeAdvisoryKey("GranitMigrationStartup"));
    }

    [Fact]
    public void ComputeAdvisoryKey_DistinctResources_DoNotCollide()
    {
        // hashtext() was int32 and collision-prone; the 64-bit SHA-256 prefix must keep
        // realistic resource names apart.
        HashSet<long> keys = [.. Enumerable.Range(0, 1000)
            .Select(i => NpgsqlAdvisoryMigrationLock.ComputeAdvisoryKey($"resource-{i}"))];

        keys.Count.ShouldBe(1000);
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
