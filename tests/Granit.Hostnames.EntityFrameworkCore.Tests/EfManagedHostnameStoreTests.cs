using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

public sealed class EfManagedHostnameStoreTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private sealed class InMemoryContextFactory(string dbName, ICurrentTenant? currentTenant = null)
        : IDbContextFactory<HostnamesDbContext>
    {
        public HostnamesDbContext CreateDbContext()
        {
            DbContextOptions<HostnamesDbContext> options =
                new DbContextOptionsBuilder<HostnamesDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options;
            return new HostnamesDbContext(options, currentTenant ?? GranitDesignTime.CurrentTenant);
        }

        public Task<HostnamesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ICurrentTenant MakeTenant(Guid id)
    {
        ICurrentTenant t = Substitute.For<ICurrentTenant>();
        t.IsAvailable.Returns(true);
        t.Id.Returns(id);
        return t;
    }

    private static EfManagedHostnameStore CreateStore(string dbName, Guid? tenantId = null)
    {
        ICurrentTenant tenant = MakeTenant(tenantId ?? TenantA);
        return new(new InMemoryContextFactory(dbName, tenant), tenant);
    }

    private static ManagedHostname MakeHostname(
        string host = "acme.com",
        string ownerType = "cms.site",
        Guid? ownerId = null,
        Guid? tenantId = null,
        bool isPrimary = false) =>
        ManagedHostname.Create(
            Guid.NewGuid(),
            host,
            ownerType,
            ownerId ?? OwnerId,
            tenantId ?? TenantA,
            isPrimary);

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        EfManagedHostnameStore store = CreateStore(nameof(GetByIdAsync_returns_null_for_unknown_id));

        ManagedHostname? result = await store.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_persisted_hostname()
    {
        EfManagedHostnameStore store = CreateStore(nameof(GetByIdAsync_returns_persisted_hostname));
        ManagedHostname hostname = MakeHostname();
        await store.AddAsync(hostname, TestContext.Current.CancellationToken);

        ManagedHostname? result = await store.GetByIdAsync(hostname.Id, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Host.Value.ShouldBe("acme.com");
    }

    // ── FindByHostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FindByHostAsync_returns_null_for_unknown_host()
    {
        EfManagedHostnameStore store = CreateStore(nameof(FindByHostAsync_returns_null_for_unknown_host));

        ManagedHostname? result = await store.FindByHostAsync("unknown.example.com", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByHostAsync_returns_null_for_invalid_fqdn()
    {
        EfManagedHostnameStore store = CreateStore(nameof(FindByHostAsync_returns_null_for_invalid_fqdn));

        ManagedHostname? result = await store.FindByHostAsync("not-a-valid!!fqdn", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByHostAsync_is_case_insensitive()
    {
        EfManagedHostnameStore store = CreateStore(nameof(FindByHostAsync_is_case_insensitive));
        await store.AddAsync(MakeHostname("acme.com"), TestContext.Current.CancellationToken);

        ManagedHostname? result = await store.FindByHostAsync("ACME.COM", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Host.Value.ShouldBe("acme.com");
    }

    [Fact]
    public async Task FindByHostAsync_crosses_tenant_boundary()
    {
        // The store is scoped to TenantA but the hostname belongs to TenantB.
        // FindByHostAsync is tenant-agnostic, so it should still find it.
        EfManagedHostnameStore storeA = CreateStore(nameof(FindByHostAsync_crosses_tenant_boundary), TenantA);
        await storeA.AddAsync(MakeHostname("tenant-b.com", tenantId: TenantB), TestContext.Current.CancellationToken);

        EfManagedHostnameStore storeAQuerying = CreateStore(nameof(FindByHostAsync_crosses_tenant_boundary), TenantA);
        ManagedHostname? result = await storeAQuerying.FindByHostAsync("tenant-b.com", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(TenantB);
    }

    // ── ListByOwnerAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListByOwnerAsync_returns_empty_for_unknown_owner()
    {
        EfManagedHostnameStore store = CreateStore(nameof(ListByOwnerAsync_returns_empty_for_unknown_owner));

        IReadOnlyList<ManagedHostname> result = await store.ListByOwnerAsync(
            "cms.site", Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListByOwnerAsync_returns_only_matching_owner()
    {
        string db = nameof(ListByOwnerAsync_returns_only_matching_owner);
        EfManagedHostnameStore store = CreateStore(db);
        var otherOwner = Guid.NewGuid();

        await store.AddAsync(MakeHostname("a.com", ownerId: OwnerId), TestContext.Current.CancellationToken);
        await store.AddAsync(MakeHostname("b.com", ownerId: OwnerId), TestContext.Current.CancellationToken);
        await store.AddAsync(MakeHostname("c.com", ownerId: otherOwner), TestContext.Current.CancellationToken);

        IReadOnlyList<ManagedHostname> result = await store.ListByOwnerAsync(
            "cms.site", OwnerId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(h => h.OwnerId == OwnerId);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_persists_changes()
    {
        EfManagedHostnameStore store = CreateStore(nameof(UpdateAsync_persists_changes));
        ManagedHostname hostname = MakeHostname();
        await store.AddAsync(hostname, TestContext.Current.CancellationToken);

        hostname.SetPrimary();
        await store.UpdateAsync(hostname, TestContext.Current.CancellationToken);

        ManagedHostname? updated = await store.GetByIdAsync(hostname.Id, TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.IsPrimary.ShouldBeTrue();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_removes_hostname()
    {
        EfManagedHostnameStore store = CreateStore(nameof(DeleteAsync_removes_hostname));
        ManagedHostname hostname = MakeHostname();
        await store.AddAsync(hostname, TestContext.Current.CancellationToken);

        await store.DeleteAsync(hostname, TestContext.Current.CancellationToken);

        ManagedHostname? result = await store.GetByIdAsync(hostname.Id, TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }
}
