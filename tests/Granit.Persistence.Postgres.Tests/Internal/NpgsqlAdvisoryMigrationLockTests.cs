using Granit.Persistence.Postgres.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Postgres.Tests.Internal;

public sealed class NpgsqlAdvisoryMigrationLockTests
{
    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        NpgsqlAdvisoryMigrationLock sut = new(
            configuration,
            NullLogger<NpgsqlAdvisoryMigrationLock>.Instance);

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
        await handle.DisposeAsync(); // NoOp — should not throw
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_DisposeAsync_DoesNotThrow()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        NpgsqlAdvisoryMigrationLock sut = new(
            configuration,
            NullLogger<NpgsqlAdvisoryMigrationLock>.Instance);

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(async () => await handle!.DisposeAsync());
    }
}
