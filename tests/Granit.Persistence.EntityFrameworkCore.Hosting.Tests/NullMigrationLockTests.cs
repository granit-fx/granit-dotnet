using Granit.Persistence.EntityFrameworkCore.Hosting.Internal;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public class NullMigrationLockTests
{
    [Fact]
    public async Task TryAcquireAsync_should_always_return_non_null_handle()
    {
        var lockInstance = new NullMigrationLock();

        IAsyncDisposable? handle = await lockInstance.TryAcquireAsync("test", CancellationToken.None);

        handle.ShouldNotBeNull();
        await handle.DisposeAsync(); // should not throw
    }
}
