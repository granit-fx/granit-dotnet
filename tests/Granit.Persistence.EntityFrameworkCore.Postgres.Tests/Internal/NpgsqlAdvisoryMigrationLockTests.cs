using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests.Internal;

public sealed class NpgsqlAdvisoryMigrationLockTests
{
    private static NpgsqlAdvisoryMigrationLock CreateLock(
        IConfiguration configuration, GranitMigrateOptions? options = null) =>
        new(configuration,
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitMigrateOptions()),
            NullLogger<NpgsqlAdvisoryMigrationLock>.Instance);

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle()
    {
        NpgsqlAdvisoryMigrationLock sut = CreateLock(new ConfigurationBuilder().Build());

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
        await handle.DisposeAsync(); // NoOp — should not throw
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_DisposeAsync_DoesNotThrow()
    {
        NpgsqlAdvisoryMigrationLock sut = CreateLock(new ConfigurationBuilder().Build());

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(async () => await handle!.DisposeAsync());
    }

    [Fact]
    public async Task TryAcquireAsync_ReadsTheConfiguredConnectionStringName()
    {
        // Only "DefaultConnection" is configured, but the options point the lock at
        // "MigrationsDb" — the lock must look up the configured NAME, not the default,
        // so it lands on the no-connection-string branch (NoOp handle).
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=somewhere;Database=db",
            })
            .Build();

        NpgsqlAdvisoryMigrationLock sut = CreateLock(
            configuration, new GranitMigrateOptions { ConnectionStringName = "MigrationsDb" });

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull(); // NoOp — "MigrationsDb" is absent from configuration
    }
}
