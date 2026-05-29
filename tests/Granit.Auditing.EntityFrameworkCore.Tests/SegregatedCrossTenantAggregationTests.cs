using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Options;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the V3 cross-tenant routing contract for Auditing under
/// <see cref="DualScopeStorageMode.Segregated"/>: writes route based on
/// <c>entry.TenantId</c>, host-admin reads materialise across host + every tenant via
/// <see cref="ITenantsAccessor"/> + <see cref="ICurrentTenant.Change"/> for SOC2.
/// </summary>
public sealed class SegregatedCrossTenantAggregationTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DataFilter _filter = new();
    private readonly IDisposable _filterDisable;

    public SegregatedCrossTenantAggregationTests()
        => _filterDisable = _filter.Disable<IMultiTenant>();

    public void Dispose() => _filterDisable.Dispose();

    [Fact]
    public async Task Writer_TenantScoped_RoutesToTenantContext()
    {
        DbContextOptions<AuditingHostDbContext> hostOpts = InMemoryHostOptions("audit-host-route");
        DbContextOptions<AuditingTenantDbContext> tenantOpts = InMemoryTenantOptions("audit-tenant-route");

        IDbContextFactory<AuditingHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<AuditingTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        AuditingContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreAuditingWriter writer = new(resolver);

        AuditEntry tenantEntry = NewEntry(_tenantA);
        await writer.WriteAsync(tenantEntry, TestContext.Current.CancellationToken);

        await using (AuditingHostDbContext hostCtx = new(hostOpts, GranitDesignTime.CurrentTenant, _filter))
        {
            (await hostCtx.AuditEntries.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
                .ShouldBe(0);
        }

        await using AuditingTenantDbContext tenantCtx = new(tenantOpts, GranitDesignTime.CurrentTenant, _filter);
        AuditEntry persisted = (await tenantCtx.AuditEntries
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem();
        persisted.Id.ShouldBe(tenantEntry.Id);
        persisted.TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task Writer_HostScoped_RoutesToHostContext()
    {
        DbContextOptions<AuditingHostDbContext> hostOpts = InMemoryHostOptions("audit-host-route-h");
        DbContextOptions<AuditingTenantDbContext> tenantOpts = InMemoryTenantOptions("audit-tenant-route-h");

        IDbContextFactory<AuditingHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<AuditingTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        AuditingContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreAuditingWriter writer = new(resolver);

        AuditEntry hostEntry = NewEntry(tenantId: null);
        await writer.WriteAsync(hostEntry, TestContext.Current.CancellationToken);

        await using AuditingHostDbContext hostCtx = new(hostOpts, GranitDesignTime.CurrentTenant, _filter);
        (await hostCtx.AuditEntries.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);

        await using AuditingTenantDbContext tenantCtx = new(tenantOpts, GranitDesignTime.CurrentTenant, _filter);
        (await tenantCtx.AuditEntries.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Reader_HostAdmin_GetByUser_MaterialisesAcrossHostPlusEveryTenant()
    {
        DbContextOptions<AuditingHostDbContext> hostOpts = InMemoryHostOptions("audit-host-mat");
        DbContextOptions<AuditingTenantDbContext> tenantOpts = InMemoryTenantOptions("audit-tenant-mat");

        AuditEntry hostEntry = NewEntry(tenantId: null, userId: "alice");
        AuditEntry tenantAEntry = NewEntry(tenantId: _tenantA, userId: "alice");
        AuditEntry tenantBEntry = NewEntry(tenantId: _tenantB, userId: "alice");

        await SeedHostAsync(hostOpts, hostEntry);
        await SeedTenantAsync(tenantOpts, tenantAEntry, tenantBEntry);

        IDbContextFactory<AuditingHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<AuditingTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        AuditingContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        ITenantsAccessor tenantsAccessor = StubTenantsAccessor(_tenantA, _tenantB);

        // Rebuild factories with the switchable currentTenant so query filters parameterise
        // on the active tenant during the materialisation loop.
        IDbContextFactory<AuditingHostDbContext> hostFactoryWithTenant = StubHostFactory(hostOpts, currentTenant);
        IDbContextFactory<AuditingTenantDbContext> tenantFactoryWithTenant = StubTenantFactory(tenantOpts, currentTenant);
        resolver = new AuditingContextResolver(DualScopeStorageMode.Segregated, hostFactoryWithTenant, tenantFactoryWithTenant);

        EfCoreAuditingReader reader = new(
            resolver,
            new FusionCache(new FusionCacheOptions()),
            currentTenant,
            tenantsAccessor,
            aliasProviders: [],
            Microsoft.Extensions.Options.Options.Create(new AuditingOptions()));

        List<AuditEntry> result = await reader.GetByUserAsync(
            "alice", limit: 100, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result.ShouldContain(e => e.TenantId == null);
        result.ShouldContain(e => e.TenantId == _tenantA);
        result.ShouldContain(e => e.TenantId == _tenantB);
    }

    // ---- helpers -----------------------------------------------------------

    private static DbContextOptions<AuditingHostDbContext> InMemoryHostOptions(string name)
        => new DbContextOptionsBuilder<AuditingHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<AuditingTenantDbContext> InMemoryTenantOptions(string name)
        => new DbContextOptionsBuilder<AuditingTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<AuditingHostDbContext> opts,
        params AuditEntry[] entries)
    {
        await using AuditingHostDbContext db = new(opts, GranitDesignTime.CurrentTenant, _filter);
        db.AuditEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<AuditingTenantDbContext> opts,
        params AuditEntry[] entries)
    {
        await using AuditingTenantDbContext db = new(opts, GranitDesignTime.CurrentTenant, _filter);
        db.AuditEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IDbContextFactory<AuditingHostDbContext> StubHostFactory(
        DbContextOptions<AuditingHostDbContext> opts, ICurrentTenant? currentTenant = null)
    {
        ICurrentTenant tenant = currentTenant ?? GranitDesignTime.CurrentTenant;
        IDbContextFactory<AuditingHostDbContext> factory = Substitute.For<IDbContextFactory<AuditingHostDbContext>>();
        factory.CreateDbContext().Returns(_ => new AuditingHostDbContext(opts, tenant));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AuditingHostDbContext(opts, tenant)));
        return factory;
    }

    private static IDbContextFactory<AuditingTenantDbContext> StubTenantFactory(
        DbContextOptions<AuditingTenantDbContext> opts, ICurrentTenant? currentTenant = null)
    {
        ICurrentTenant tenant = currentTenant ?? GranitDesignTime.CurrentTenant;
        IDbContextFactory<AuditingTenantDbContext> factory = Substitute.For<IDbContextFactory<AuditingTenantDbContext>>();
        factory.CreateDbContext().Returns(_ => new AuditingTenantDbContext(opts, tenant));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AuditingTenantDbContext(opts, tenant)));
        return factory;
    }

    private static ITenantsAccessor StubTenantsAccessor(params Guid[] tenantIds)
    {
        ITenantsAccessor accessor = Substitute.For<ITenantsAccessor>();
        (Guid Id, string Name)[] tenants = [.. tenantIds.Select(id => (id, $"tenant-{id}"))];
        accessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>(tenants));
        return accessor;
    }

    private static ICurrentTenant NewSwitchableTenant(bool initiallyAvailable)
    {
        Guid? currentId = null;
        bool available = initiallyAvailable;

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(_ => available);
        tenant.Id.Returns(_ => currentId);
        tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(call =>
            {
                Guid? previousId = currentId;
                bool previousAvail = available;
                currentId = call.ArgAt<Guid?>(0);
                available = currentId is not null;
                return new ChangeScope(() => { currentId = previousId; available = previousAvail; });
            });
        return tenant;
    }

    private sealed class ChangeScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private static AuditEntry NewEntry(Guid? tenantId, string userId = "user-1") => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        UserId = userId,
        Category = AuditCategory.ConfigurationChange,
        TenantId = tenantId,
    };
}
