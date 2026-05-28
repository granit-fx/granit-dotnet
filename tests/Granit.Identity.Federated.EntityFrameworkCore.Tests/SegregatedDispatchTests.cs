using Granit.Domain;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the Phase B Segregated dispatch contract: <see cref="EfCoreUserCacheStore"/>
/// routes writes to the host context when <c>tenantId</c> is null and to the tenant
/// context otherwise; <see cref="EfCoreUserCacheStore.FindFirstByExternalIdAsync"/>
/// iterates every tenant returned by <see cref="ITenantsAccessor"/> under host-admin
/// scope.
/// </summary>
public sealed class SegregatedDispatchTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly TestDataFilter _filter = new();

    public void Dispose() => _filter.Dispose();

    [Fact]
    public async Task FindFirstByExternalIdAsync_HostAdmin_ProbesHostThenEveryTenant()
    {
        DbContextOptions<IdentityFederatedHostDbContext> hostOpts = HostOpts("host-find");
        DbContextOptions<IdentityFederatedTenantDbContext> tenantOpts = TenantOpts("tenant-find");

        await SeedHostAsync(hostOpts);
        await SeedTenantAsync(tenantOpts,
            CreateEntry("federated-user-1", _tenantA));

        TestIdentityFederatedHostDbContextFactory hostFactory = new(hostOpts, _filter.Filter);
        TestIdentityFederatedTenantDbContextFactory tenantFactory = new(tenantOpts, _filter.Filter);
        IdentityFederatedContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        ICurrentTenant currentTenant = NewSwitchableTenant();
        ITenantsAccessor accessor = StubAccessor(_tenantA, _tenantB);

        EfCoreUserCacheStore sut = new(resolver, StubHasher(), currentTenant, accessor);

        FederatedIdentity? result = await sut.FindFirstByExternalIdAsync(
            "federated-user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task UpsertAsync_TenantScopedEntry_GoesToTenantContext_UnderSegregated()
    {
        DbContextOptions<IdentityFederatedHostDbContext> hostOpts = HostOpts("host-upsert");
        DbContextOptions<IdentityFederatedTenantDbContext> tenantOpts = TenantOpts("tenant-upsert");

        TestIdentityFederatedHostDbContextFactory hostFactory = new(hostOpts, _filter.Filter);
        TestIdentityFederatedTenantDbContextFactory tenantFactory = new(tenantOpts, _filter.Filter);
        IdentityFederatedContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        ICurrentTenant currentTenant = NewSwitchableTenant();
        ITenantsAccessor accessor = StubAccessor(_tenantA);

        EfCoreUserCacheStore sut = new(resolver, StubHasher(), currentTenant, accessor);

        await sut.UpsertAsync(CreateEntry("tenant-A-user", _tenantA), TestContext.Current.CancellationToken);

        // Inspect each context directly: tenant DB should contain the row, host should be empty.
        await using IdentityFederatedTenantDbContext tenantCheck = new(tenantOpts, GranitDesignTime.CurrentTenant, _filter.Filter);
        (await tenantCheck.FederatedIdentities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        await using IdentityFederatedHostDbContext hostCheck = new(hostOpts, GranitDesignTime.CurrentTenant, _filter.Filter);
        (await hostCheck.FederatedIdentities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task UpsertAsync_HostScopedEntry_GoesToHostContext_UnderSegregated()
    {
        DbContextOptions<IdentityFederatedHostDbContext> hostOpts = HostOpts("host-only-upsert");
        DbContextOptions<IdentityFederatedTenantDbContext> tenantOpts = TenantOpts("tenant-untouched");

        TestIdentityFederatedHostDbContextFactory hostFactory = new(hostOpts, _filter.Filter);
        TestIdentityFederatedTenantDbContextFactory tenantFactory = new(tenantOpts, _filter.Filter);
        IdentityFederatedContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        ICurrentTenant currentTenant = NewSwitchableTenant();
        ITenantsAccessor accessor = StubAccessor();

        EfCoreUserCacheStore sut = new(resolver, StubHasher(), currentTenant, accessor);

        await sut.UpsertAsync(CreateEntry("host-admin-user", tenantId: null), TestContext.Current.CancellationToken);

        await using IdentityFederatedHostDbContext hostCheck = new(hostOpts, GranitDesignTime.CurrentTenant, _filter.Filter);
        (await hostCheck.FederatedIdentities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        await using IdentityFederatedTenantDbContext tenantCheck = new(tenantOpts, GranitDesignTime.CurrentTenant, _filter.Filter);
        (await tenantCheck.FederatedIdentities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    // ---- helpers ------------------------------------------------------------

    private static DbContextOptions<IdentityFederatedHostDbContext> HostOpts(string name)
        => new DbContextOptionsBuilder<IdentityFederatedHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<IdentityFederatedTenantDbContext> TenantOpts(string name)
        => new DbContextOptionsBuilder<IdentityFederatedTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<IdentityFederatedHostDbContext> opts,
        params FederatedIdentity[] entries)
    {
        using IDisposable _ = _filter.Filter.Disable<IMultiTenant>();
        await using IdentityFederatedHostDbContext db = new(opts, GranitDesignTime.CurrentTenant, _filter.Filter);
        db.FederatedIdentities.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<IdentityFederatedTenantDbContext> opts,
        params FederatedIdentity[] entries)
    {
        using IDisposable _ = _filter.Filter.Disable<IMultiTenant>();
        await using IdentityFederatedTenantDbContext db = new(opts, GranitDesignTime.CurrentTenant, _filter.Filter);
        db.FederatedIdentities.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IUserLookupHasher StubHasher()
    {
        IUserLookupHasher hasher = Substitute.For<IUserLookupHasher>();
        hasher.ComputeEmailHash(Arg.Any<string?>())
            .Returns(ci => ci.Arg<string?>() is { Length: > 0 } email ? "hash:" + email : null);
        return hasher;
    }

    private static ITenantsAccessor StubAccessor(params Guid[] tenantIds)
    {
        ITenantsAccessor accessor = Substitute.For<ITenantsAccessor>();
        (Guid Id, string Name)[] tenants = [.. tenantIds.Select(id => (id, $"tenant-{id}"))];
        accessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>(tenants));
        return accessor;
    }

    private static ICurrentTenant NewSwitchableTenant()
    {
        Guid? currentId = null;
        bool available = false;
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(_ => available);
        tenant.Id.Returns(_ => currentId);
        tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(call =>
            {
                Guid? prevId = currentId;
                bool prevAvail = available;
                currentId = call.ArgAt<Guid?>(0);
                available = currentId is not null;
                return new ChangeScope(() => { currentId = prevId; available = prevAvail; });
            });
        return tenant;
    }

    private sealed class ChangeScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private static FederatedIdentity CreateEntry(string externalUserId, Guid? tenantId) => new()
    {
        Id = Guid.NewGuid(),
        ExternalUserId = externalUserId,
        Username = "user",
        Email = $"{externalUserId}@example.com",
        FirstName = "Test",
        LastName = "User",
        Enabled = true,
        LastSyncedAt = DateTimeOffset.UtcNow,
        TenantId = tenantId,
    };
}
