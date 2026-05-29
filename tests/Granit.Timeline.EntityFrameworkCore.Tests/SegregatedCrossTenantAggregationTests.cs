using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Granit.Timeline.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the cross-tenant aggregation contract for V3 of Epic #2382: under
/// <see cref="DualScopeStorageMode.Segregated"/> + host-admin scope,
/// <see cref="EfCoreTimelineQuery"/> iterates every tenant via
/// <see cref="ITenantsAccessor"/> + <see cref="ICurrentTenant.Change"/> to materialise the
/// timeline stream across host + every tenant DB.
/// </summary>
public sealed class SegregatedCrossTenantAggregationTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DataFilter _filter = new();

    [Fact]
    public async Task TimelineQuery_HostAdmin_MaterialisesHostPlusEveryTenant()
    {
        DbContextOptions<TimelineHostDbContext> hostOpts = InMemoryHostOptions("tl-host");
        DbContextOptions<TimelineTenantDbContext> tenantOpts = InMemoryTenantOptions("tl-tenant");

        await SeedHostAsync(hostOpts, NewEntry("Patient", "p-1", "Host announcement", tenantId: null));
        await SeedTenantAsync(tenantOpts,
            NewEntry("Patient", "p-1", "Tenant A comment", _tenantA),
            NewEntry("Patient", "p-1", "Tenant B comment", _tenantB));

        IDbContextFactory<TimelineHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<TimelineTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);
        ITenantsAccessor tenantsAccessor = StubTenantsAccessor(_tenantA, _tenantB);
        TimelineContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreTimelineQuery sut = new(
            resolver,
            hostFactory,
            currentTenant,
            tenantsAccessor,
            sources: [],
            Microsoft.Extensions.Options.Options.Create(new TimelineOptions()),
            NullLogger<EfCoreTimelineQuery>.Instance,
            tenantFactory);

        TimelineStreamResult result = await sut.GetStreamAsync(
            "Patient", "p-1", page: 1, pageSize: 50, cancellationToken: TestContext.Current.CancellationToken);

        result.Page.TotalCount.ShouldBe(3);
        result.Page.Items.Count.ShouldBe(3);
        result.Page.Items.ShouldContain(e => e.Body == "Host announcement");
        result.Page.Items.ShouldContain(e => e.Body == "Tenant A comment");
        result.Page.Items.ShouldContain(e => e.Body == "Tenant B comment");
    }

    [Fact]
    public async Task TimelineQuery_HostAdmin_EmptyTenantsAccessor_ReturnsHostOnly()
    {
        DbContextOptions<TimelineHostDbContext> hostOpts = InMemoryHostOptions("tl-host-noaccessor");
        DbContextOptions<TimelineTenantDbContext> tenantOpts = InMemoryTenantOptions("tl-tenant-noaccessor");

        await SeedHostAsync(hostOpts, NewEntry("Patient", "p-1", "Host only", tenantId: null));
        await SeedTenantAsync(tenantOpts, NewEntry("Patient", "p-1", "Ghost", _tenantA));

        IDbContextFactory<TimelineHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<TimelineTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);
        TimelineContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreTimelineQuery sut = new(
            resolver,
            hostFactory,
            currentTenant,
            tenantsAccessor: StubTenantsAccessor() /* empty — NullTenantsAccessor-style */,
            sources: [],
            Microsoft.Extensions.Options.Options.Create(new TimelineOptions()),
            NullLogger<EfCoreTimelineQuery>.Instance,
            tenantFactory);

        TimelineStreamResult result = await sut.GetStreamAsync(
            "Patient", "p-1", page: 1, pageSize: 50, cancellationToken: TestContext.Current.CancellationToken);

        result.Page.TotalCount.ShouldBe(1);
        result.Page.Items.Count.ShouldBe(1);
        result.Page.Items[0].Body.ShouldBe("Host only");
    }

    // ---- helpers -----------------------------------------------------------

    private static DbContextOptions<TimelineHostDbContext> InMemoryHostOptions(string name)
        => new DbContextOptionsBuilder<TimelineHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<TimelineTenantDbContext> InMemoryTenantOptions(string name)
        => new DbContextOptionsBuilder<TimelineTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<TimelineHostDbContext> opts,
        params TimelineEntry[] entries)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using TimelineHostDbContext db = new(opts, tenant, _filter);
        db.TimelineEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<TimelineTenantDbContext> opts,
        params TimelineEntry[] entries)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using TimelineTenantDbContext db = new(opts, tenant, _filter);
        db.TimelineEntries.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IDbContextFactory<TimelineHostDbContext> StubHostFactory(
        DbContextOptions<TimelineHostDbContext> opts)
    {
        IDbContextFactory<TimelineHostDbContext> factory = Substitute.For<IDbContextFactory<TimelineHostDbContext>>();
        factory.CreateDbContext().Returns(_ => new TimelineHostDbContext(opts, GranitDesignTime.CurrentTenant));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new TimelineHostDbContext(opts, GranitDesignTime.CurrentTenant)));
        return factory;
    }

    private IDbContextFactory<TimelineTenantDbContext> StubTenantFactory(
        DbContextOptions<TimelineTenantDbContext> opts,
        ICurrentTenant currentTenant)
    {
        IDbContextFactory<TimelineTenantDbContext> factory = Substitute.For<IDbContextFactory<TimelineTenantDbContext>>();
        factory.CreateDbContext().Returns(_ => new TimelineTenantDbContext(opts, currentTenant, _filter));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new TimelineTenantDbContext(opts, currentTenant, _filter)));
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

    private static TimelineEntry NewEntry(string entityType, string entityId, string body, Guid? tenantId)
        => TimelineEntry.Create(
            id: Guid.NewGuid(),
            entity: new EntityReference(entityType, entityId),
            entryType: TimelineEntryType.Comment,
            body: body,
            author: new AuthorInfo("test-user", "Test User"),
            createdAt: DateTimeOffset.UtcNow,
            createdBy: "test-user",
            tenantId: tenantId);
}
