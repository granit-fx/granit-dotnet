using Granit.Indexing.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.Indexing.BackgroundJobs.Tests;

public sealed class InMemoryRebuildCheckpointStoreTests
{
    private static readonly Guid TenantA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetLastCheckpointAsync_returns_default_when_no_checkpoint_set()
    {
        // Quirk of generic nullable annotations on value types: with `TKey : notnull`,
        // `TKey?` collapses to `TKey` (non-nullable) for value-type instantiations.
        // The "no checkpoint" sentinel for Guid is therefore default(Guid) = Guid.Empty,
        // NOT null. Consumers check `if (value == default(TKey))`. Documented here so a
        // future refactor that tries to "fix" the test by switching to null gets caught.
        InMemoryRebuildCheckpointStore<Guid> store = new();

        Guid result = await store.GetLastCheckpointAsync(TenantA, "documents", TestContext.Current.CancellationToken);

        result.ShouldBe(default(Guid));
    }

    [Fact]
    public async Task SetCheckpoint_then_GetLastCheckpointAsync_returns_the_value()
    {
        InMemoryRebuildCheckpointStore<Guid> store = new();
        var checkpoint = Guid.NewGuid();

        await store.SetCheckpointAsync(TenantA, "documents", checkpoint, TestContext.Current.CancellationToken);
        Guid retrieved = await store.GetLastCheckpointAsync(TenantA, "documents", TestContext.Current.CancellationToken);

        retrieved.ShouldBe(checkpoint);
    }

    [Fact]
    public async Task Checkpoints_are_partitioned_by_tenant_and_source_name()
    {
        // Two tenants writing to the same source must NOT see each other's checkpoint.
        // A single rebuild run for tenant A finishing must not nuke tenant B's progress.
        InMemoryRebuildCheckpointStore<Guid> store = new();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await store.SetCheckpointAsync(TenantA, "documents", a, TestContext.Current.CancellationToken);
        await store.SetCheckpointAsync(TenantB, "documents", b, TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(TenantA, "documents", TestContext.Current.CancellationToken)).ShouldBe(a);
        (await store.GetLastCheckpointAsync(TenantB, "documents", TestContext.Current.CancellationToken)).ShouldBe(b);
    }

    [Fact]
    public async Task ClearAsync_returns_to_default()
    {
        InMemoryRebuildCheckpointStore<Guid> store = new();
        await store.SetCheckpointAsync(TenantA, "documents", Guid.NewGuid(), TestContext.Current.CancellationToken);

        await store.ClearAsync(TenantA, "documents", TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(TenantA, "documents", TestContext.Current.CancellationToken)).ShouldBe(default(Guid));
    }

    [Fact]
    public async Task Null_tenantId_is_a_distinct_partition_from_concrete_tenants()
    {
        // null tenant = ops-driven cross-tenant rebuild. The store key must distinguish
        // it from any concrete tenant so a global rebuild doesn't accidentally read or
        // overwrite a tenant-scoped checkpoint.
        InMemoryRebuildCheckpointStore<Guid> store = new();
        var g = Guid.NewGuid();
        var t = Guid.NewGuid();

        await store.SetCheckpointAsync(null, "documents", g, TestContext.Current.CancellationToken);
        await store.SetCheckpointAsync(TenantA, "documents", t, TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(null, "documents", TestContext.Current.CancellationToken)).ShouldBe(g);
        (await store.GetLastCheckpointAsync(TenantA, "documents", TestContext.Current.CancellationToken)).ShouldBe(t);
    }
}
