// EF1001: instantiates an "Internal" namespace type. Acceptable here because the
// store is the unit under test and we own the assembly via InternalsVisibleTo —
// the analyzer is a cross-package guard, not a real access boundary.
#pragma warning disable EF1001
using Granit.Indexing.EntityFrameworkCore.Internal;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the cross-tenant isolation invariant for <see cref="EfRebuildCheckpointStore{TKey}"/>.
/// The store deliberately bypasses <c>GranitFilterNames.MultiTenant</c> on every read/write
/// because the rebuild job's target tenant id is dictated by the message payload, which may
/// differ from the ambient <c>ICurrentTenant</c>. Isolation rests entirely on the explicit
/// <c>r.TenantId == tenantId</c> equality predicate — these tests fail loudly if a future
/// refactor drops it.
/// </summary>
public sealed class EfRebuildCheckpointStoreCrossTenantIsolationTests
{
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string SourceName = "documents";

    [Fact]
    public async Task GetLastCheckpointAsync_does_not_return_another_tenants_row_with_same_source()
    {
        // Both tenants own a checkpoint row for the same source. Reading tenant A MUST
        // return tenant A's value — never tenant B's, never the ambient tenant's, never
        // the first row encountered.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken, typeof(Guid));

        var store = new EfRebuildCheckpointStore<Guid>(h.Factory, TimeProvider.System);
        var checkpointA = Guid.NewGuid();
        var checkpointB = Guid.NewGuid();

        await store.SetCheckpointAsync(TenantA, SourceName, checkpointA, TestContext.Current.CancellationToken);
        await store.SetCheckpointAsync(TenantB, SourceName, checkpointB, TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(TenantA, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(checkpointA);
        (await store.GetLastCheckpointAsync(TenantB, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(checkpointB);
    }

    [Fact]
    public async Task ClearAsync_does_not_delete_another_tenants_row()
    {
        // ExecuteDelete is the most dangerous variant — it bypasses change tracking. A
        // missing predicate would wipe every row matching SourceName across all tenants.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken, typeof(Guid));

        var store = new EfRebuildCheckpointStore<Guid>(h.Factory, TimeProvider.System);
        var checkpointA = Guid.NewGuid();
        var checkpointB = Guid.NewGuid();

        await store.SetCheckpointAsync(TenantA, SourceName, checkpointA, TestContext.Current.CancellationToken);
        await store.SetCheckpointAsync(TenantB, SourceName, checkpointB, TestContext.Current.CancellationToken);

        await store.ClearAsync(TenantA, SourceName, TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(TenantA, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(default);
        // Tenant B's row must survive.
        (await store.GetLastCheckpointAsync(TenantB, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(checkpointB);
    }

    [Fact]
    public async Task SetCheckpointAsync_for_ambient_tenant_A_can_write_for_a_different_tenant_B()
    {
        // The store explicitly bypasses the tenant filter so an ops-driven cross-tenant
        // rebuild can checkpoint against a tenant other than the ambient one. This is
        // intentional, but it MUST require explicit equality (locked here).
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken, typeof(Guid));

        var store = new EfRebuildCheckpointStore<Guid>(h.Factory, TimeProvider.System);
        var checkpointForB = Guid.NewGuid();

        await store.SetCheckpointAsync(TenantB, SourceName, checkpointForB, TestContext.Current.CancellationToken);

        (await store.GetLastCheckpointAsync(TenantB, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(checkpointForB);
        (await store.GetLastCheckpointAsync(TenantA, SourceName, TestContext.Current.CancellationToken))
            .ShouldBe(default);
    }

    [Fact]
    public async Task ConcurrencyStamp_blocks_a_stale_update_so_duplicate_dispatch_cannot_clobber()
    {
        // Locks the EF concurrency wiring: a stale OriginalValue on ConcurrencyStamp
        // must surface DbUpdateConcurrencyException, which the store maps to
        // RebuildAlreadyInProgressException for the dead-letter path.
        await using SqliteHarness h = await SqliteHarness.CreateAsync(
            new FakeCurrentTenant { Id = TenantA }, TestContext.Current.CancellationToken, typeof(Guid));

        var store = new EfRebuildCheckpointStore<Guid>(h.Factory, TimeProvider.System);
        await store.SetCheckpointAsync(TenantA, SourceName, Guid.NewGuid(), TestContext.Current.CancellationToken);

        await using IndexingDbContext stale = await h.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        IndexingRebuildCheckpointRow staleRow = await stale.Set<IndexingRebuildCheckpointRow>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .FirstAsync(r => r.TenantId == TenantA && r.SourceName == SourceName, TestContext.Current.CancellationToken);

        // Simulate a competing writer that has already moved the stamp forward.
        stale.Entry(staleRow).Property(e => e.ConcurrencyStamp).OriginalValue = Guid.NewGuid().ToString();
        staleRow.LastProcessedKey = Guid.NewGuid().ToString();

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => stale.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
