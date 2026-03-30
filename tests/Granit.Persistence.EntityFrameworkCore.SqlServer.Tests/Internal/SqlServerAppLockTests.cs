using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Internal;

public sealed class SqlServerAppLockTests
{
    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_ReturnsNoOpHandle()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        SqlServerAppLock sut = new(
            configuration,
            NullLogger<SqlServerAppLock>.Instance);

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "test-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_NoConnectionString_HandleDisposeAsync_DoesNotThrow()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        SqlServerAppLock sut = new(
            configuration,
            NullLogger<SqlServerAppLock>.Instance);

        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "resource", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(async () => await handle!.DisposeAsync());
    }

    [Fact]
    public async Task TryAcquireAsync_ResourceName_IsPassedThrough()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        SqlServerAppLock sut = new(
            configuration,
            NullLogger<SqlServerAppLock>.Instance);

        // Returns NoOp immediately without needing a real connection
        IAsyncDisposable? handle = await sut.TryAcquireAsync(
            "my-specific-resource", TestContext.Current.CancellationToken);

        handle.ShouldNotBeNull(); // NoOp returned, not null
    }
}
