using Granit.Persistence.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Suites;

/// <summary>
/// Proves the provider's distributed migration lock with real contention: two independent
/// lock instances (two "replicas") on the same database — exactly one acquires. This is the
/// execution-level coverage whose absence let a dead lock registration ship unnoticed.
/// </summary>
public abstract class MigrationLockConformanceSuite(IRelationalConformanceFixture fixture)
{
    [Fact]
    public async Task Two_contenders_on_one_resource_only_one_acquires()
    {
        IGranitMigrationLock replica1 = fixture.CreateMigrationLock();
        IGranitMigrationLock replica2 = fixture.CreateMigrationLock();
        string resource = $"conformance-{Guid.CreateVersion7():N}";

        IAsyncDisposable? first = await replica1.TryAcquireAsync(resource, TestContext.Current.CancellationToken);
        first.ShouldNotBeNull($"{fixture.ProviderName}: the first contender must acquire the lock");

        IAsyncDisposable? second = await replica2.TryAcquireAsync(resource, TestContext.Current.CancellationToken);
        second.ShouldBeNull($"{fixture.ProviderName}: the second contender must be refused while the lock is held");

        await first.DisposeAsync();

        IAsyncDisposable? afterRelease = await replica2.TryAcquireAsync(resource, TestContext.Current.CancellationToken);
        afterRelease.ShouldNotBeNull($"{fixture.ProviderName}: the lock must be acquirable after release");
        await afterRelease.DisposeAsync();
    }

    [Fact]
    public async Task Distinct_resources_do_not_contend()
    {
        IGranitMigrationLock replica1 = fixture.CreateMigrationLock();
        IGranitMigrationLock replica2 = fixture.CreateMigrationLock();
        string suffix = $"{Guid.CreateVersion7():N}";

        IAsyncDisposable? first = await replica1.TryAcquireAsync($"conformance-a-{suffix}", TestContext.Current.CancellationToken);
        IAsyncDisposable? second = await replica2.TryAcquireAsync($"conformance-b-{suffix}", TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull(
            $"{fixture.ProviderName}: locks on distinct resources must not contend");

        await first.DisposeAsync();
        await second.DisposeAsync();
    }
}
