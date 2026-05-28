using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the Phase 2C cross-tenant aggregation contract: under Segregated + host-admin
/// scope, <see cref="EfWebhookSubscriptionQueryableSource"/>,
/// <see cref="EfWebhookDeliveryAttemptQueryableSource"/> and
/// <see cref="EfWebhookStatsReader"/> iterate every tenant via
/// <see cref="ITenantEnumerator"/> + <see cref="ICurrentTenant.Change"/>.
/// </summary>
public sealed class SegregatedCrossTenantAggregationTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DataFilter _filter = new();

    [Fact]
    public async Task SubscriptionQueryable_HostAdmin_MaterialisesHostPlusEveryTenant()
    {
        DbContextOptions<WebhooksHostDbContext> hostOpts = InMemoryHostOptions("subs-host");
        DbContextOptions<WebhooksTenantDbContext> tenantOpts = InMemoryTenantOptions("subs-tenant");

        // Seed: 1 host sub + 1 tenant-A sub + 1 tenant-B sub. We seed the tenant DB ONCE
        // with both rows even though in production they'd live in distinct schemas — the
        // InMemory provider has no schema concept, so the test stub returns the same DB
        // for any tenant scope. The IMultiTenant row-level filter on the tenant DB ensures
        // only the active-tenant's rows are visible per iteration.
        await SeedHostAsync(hostOpts, CreateSubscription("evt", tenantId: null));
        await SeedTenantAsync(tenantOpts,
            CreateSubscription("evt", _tenantA),
            CreateSubscription("evt", _tenantB));

        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);
        ITenantEnumerator tenantEnumerator = StubTenantEnumerator(_tenantA, _tenantB);

        EfWebhookSubscriptionQueryableSource sut = new(
            new WebhooksEntityFrameworkCoreOptions { StorageMode = DualScopeStorageMode.Segregated },
            currentTenant,
            tenantEnumerator,
            hostFactory,
            tenantFactory);

        WebhookSubscription[] result = [.. sut.GetQueryable()];

        // Host row + tenant A row (active when iterating) + tenant B row (active when iterating).
        result.Length.ShouldBe(3);
        result.Count(s => s.TenantId is null).ShouldBe(1);
        result.Count(s => s.TenantId == _tenantA).ShouldBe(1);
        result.Count(s => s.TenantId == _tenantB).ShouldBe(1);
    }

    [Fact]
    public async Task StatsReader_HostAdmin_AggregatesAcrossAllTenants()
    {
        DbContextOptions<WebhooksHostDbContext> hostOpts = InMemoryHostOptions("stats-host");
        DbContextOptions<WebhooksTenantDbContext> tenantOpts = InMemoryTenantOptions("stats-tenant");

        await SeedHostAsync(hostOpts, CreateSubscription("evt", tenantId: null));
        await SeedTenantAsync(tenantOpts,
            CreateSubscription("evt", _tenantA),
            CreateSubscription("evt", _tenantB),
            CreateSubscription("evt", _tenantB));

        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);
        ITenantEnumerator tenantEnumerator = StubTenantEnumerator(_tenantA, _tenantB);
        WebhooksContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));

        EfWebhookStatsReader sut = new(resolver, currentTenant, tenantEnumerator, clock);

        Webhooks.Abstractions.WebhookStats stats = await sut.GetStatsAsync(TestContext.Current.CancellationToken);

        // 1 host + 1 tenant-A + 2 tenant-B subscriptions.
        stats.TotalSubscriptions.ShouldBe(4);
    }

    [Fact]
    public async Task SubscriptionQueryable_HostAdmin_EmptyTenantEnumerator_ReturnsHostOnly()
    {
        DbContextOptions<WebhooksHostDbContext> hostOpts = InMemoryHostOptions("subs-host-noreader");
        DbContextOptions<WebhooksTenantDbContext> tenantOpts = InMemoryTenantOptions("subs-tenant-noreader");

        await SeedHostAsync(hostOpts, CreateSubscription("evt", tenantId: null));
        await SeedTenantAsync(tenantOpts, CreateSubscription("evt", _tenantA));

        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);

        // NullTenantEnumerator-style stub (empty list) → fallback to host-only.
        EfWebhookSubscriptionQueryableSource sut = new(
            new WebhooksEntityFrameworkCoreOptions { StorageMode = DualScopeStorageMode.Segregated },
            currentTenant,
            tenantEnumerator: StubTenantEnumerator() /* empty */,
            hostFactory,
            tenantFactory);

        WebhookSubscription[] result = [.. sut.GetQueryable()];

        result.Length.ShouldBe(1);
        result[0].TenantId.ShouldBeNull();
    }

    // ---- helpers ------------------------------------------------------------

    private static DbContextOptions<WebhooksHostDbContext> InMemoryHostOptions(string name)
        => new DbContextOptionsBuilder<WebhooksHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<WebhooksTenantDbContext> InMemoryTenantOptions(string name)
        => new DbContextOptionsBuilder<WebhooksTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<WebhooksHostDbContext> opts,
        params WebhookSubscription[] subscriptions)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using WebhooksHostDbContext db = new(opts, tenant, _filter);
        db.WebhookSubscriptions.AddRange(subscriptions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<WebhooksTenantDbContext> opts,
        params WebhookSubscription[] subscriptions)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using WebhooksTenantDbContext db = new(opts, tenant, _filter);
        db.WebhookSubscriptions.AddRange(subscriptions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IDbContextFactory<WebhooksHostDbContext> StubHostFactory(
        DbContextOptions<WebhooksHostDbContext> opts)
    {
        IDbContextFactory<WebhooksHostDbContext> factory = Substitute.For<IDbContextFactory<WebhooksHostDbContext>>();
        factory.CreateDbContext().Returns(_ => new WebhooksHostDbContext(opts, GranitDesignTime.CurrentTenant));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new WebhooksHostDbContext(opts, GranitDesignTime.CurrentTenant)));
        return factory;
    }

    private IDbContextFactory<WebhooksTenantDbContext> StubTenantFactory(
        DbContextOptions<WebhooksTenantDbContext> opts,
        ICurrentTenant currentTenant)
    {
        IDbContextFactory<WebhooksTenantDbContext> factory = Substitute.For<IDbContextFactory<WebhooksTenantDbContext>>();
        factory.CreateDbContext().Returns(_ => new WebhooksTenantDbContext(opts, currentTenant, _filter));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new WebhooksTenantDbContext(opts, currentTenant, _filter)));
        return factory;
    }

    private static ITenantEnumerator StubTenantEnumerator(params Guid[] tenantIds)
    {
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        (Guid Id, string Name)[] tenants = [.. tenantIds.Select(id => (id, $"tenant-{id}"))];
        enumerator.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>(tenants));
        return enumerator;
    }

    private static ICurrentTenant NewSwitchableTenant(bool initiallyAvailable)
    {
        // A minimal ambient-style stub: Change() flips the IsAvailable/Id to the new value
        // and the returned IDisposable restores the previous values.
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

    private static WebhookSubscription CreateSubscription(string eventType, Guid? tenantId) =>
        WebhookSubscription.Create(
            Guid.NewGuid(),
            $"https://example.com/{Guid.NewGuid()}",
            eventType,
            signingKeyId: Guid.NewGuid(),
            protectedSecret: "protected-secret",
            createdAt: DateTimeOffset.UtcNow,
            tenantId: tenantId);
}
