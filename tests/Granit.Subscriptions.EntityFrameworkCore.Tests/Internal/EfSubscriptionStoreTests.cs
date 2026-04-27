using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Internal;

[Collection(SubscriptionsDbSerialGroup.Name)]
public sealed class EfSubscriptionStoreTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory;
    private readonly ICurrentTenant _currentTenant;
    private readonly EfSubscriptionReader _reader;
    private readonly EfSubscriptionWriter _writer;

    public EfSubscriptionStoreTests()
    {
        DbContextOptions<SubscriptionsDbContext> options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseInMemoryDatabase($"subscriptions-sub-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _currentTenant = Substitute.For<ICurrentTenant>();
        _factory = new TestDbContextFactory(options, _currentTenant);
        _reader = new EfSubscriptionReader(_factory, _currentTenant);
        _writer = new EfSubscriptionWriter(_factory, _currentTenant);
    }

    public async ValueTask DisposeAsync()
    {
        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static Subscription NewSubscription(
        Guid tenantId,
        SubscriptionStatus? forceStatus = null,
        DateTimeOffset? trialEndsAt = null,
        DateTimeOffset? periodEnd = null,
        bool cancelAtPeriodEnd = false,
        Guid? planId = null)
    {
        var period = new SubscriptionPeriod(
            Now.AddMonths(-1),
            periodEnd ?? Now.AddMonths(1),
            BillingCycleAnchor: Now.AddMonths(-1));

        var sub = Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            PartyId.Create(Guid.NewGuid()),
            PlanId.Create(planId ?? Guid.NewGuid()),
            "EUR",
            period,
            trialEndsAt: trialEndsAt);

        if (cancelAtPeriodEnd)
        {
            sub.ScheduleCancelAtPeriodEnd();
        }

        if (forceStatus is { } target && sub.Status != target)
        {
            ApplyStatus(sub, target);
        }

        return sub;
    }

    private static void ApplyStatus(Subscription sub, SubscriptionStatus target)
    {
        // Walk the FSM to reach the desired state (Trial/Active are the natural starts).
        switch (target)
        {
            case SubscriptionStatus.Active:
                if (sub.Status == SubscriptionStatus.Trial) { sub.Activate(); }
                break;
            case SubscriptionStatus.PastDue:
                if (sub.Status == SubscriptionStatus.Trial) { sub.Activate(); }
                sub.MarkPastDue();
                break;
            case SubscriptionStatus.Cancelled:
                if (sub.Status == SubscriptionStatus.Trial) { sub.Activate(); }
                sub.Cancel("test", Now);
                break;
        }
    }

    [Fact]
    public async Task GetByIdAsync_ExistingSubscription_Returns()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = NewSubscription(tenantId);
        await ((ISubscriptionWriter)_writer).AddAsync(sub, TestContext.Current.CancellationToken);

        Subscription? loaded = await _reader.GetByIdAsync(
            SubscriptionId.Create(sub.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task GetActiveForTenantAsync_PrefersActiveAndTrial()
    {
        var tenantId = Guid.NewGuid();
        Subscription cancelled = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Cancelled);
        Subscription active = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active);

        await ((ISubscriptionWriter)_writer).AddAsync(cancelled, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(active, TestContext.Current.CancellationToken);

        Subscription? result = await _reader.GetActiveForTenantAsync(
            tenantId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task GetByTenantAsync_ReturnsAllForTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await ((ISubscriptionWriter)_writer).AddAsync(NewSubscription(tenantA), TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(NewSubscription(tenantA), TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(NewSubscription(tenantB), TestContext.Current.CancellationToken);

        IReadOnlyList<Subscription> result = await _reader.GetByTenantAsync(
            tenantA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(s => s.TenantId == tenantA);
    }

    [Fact]
    public async Task GetExpiringTrialsAsync_FiltersOnTrialAndThreshold()
    {
        var tenantId = Guid.NewGuid();
        Subscription trialExpiring = NewSubscription(tenantId, trialEndsAt: Now.AddDays(1));
        Subscription trialFar = NewSubscription(tenantId, trialEndsAt: Now.AddDays(30));
        await ((ISubscriptionWriter)_writer).AddAsync(trialExpiring, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(trialFar, TestContext.Current.CancellationToken);

        IReadOnlyList<Subscription> result = await _reader.GetExpiringTrialsAsync(
            Now.AddDays(7), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(trialExpiring.Id);
    }

    [Fact]
    public async Task GetAtPeriodEndAsync_FiltersOnActiveAndPeriodEnd()
    {
        var tenantId = Guid.NewGuid();
        Subscription dueSoon = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active, periodEnd: Now.AddDays(1));
        Subscription dueLater = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active, periodEnd: Now.AddDays(60));
        await ((ISubscriptionWriter)_writer).AddAsync(dueSoon, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(dueLater, TestContext.Current.CancellationToken);

        IReadOnlyList<Subscription> result = await _reader.GetAtPeriodEndAsync(
            Now.AddDays(7), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(dueSoon.Id);
    }

    [Fact]
    public async Task GetPendingCancelAtPeriodEndAsync_RequiresFlagAndDuePeriod()
    {
        var tenantId = Guid.NewGuid();
        Subscription pending = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active,
            periodEnd: Now.AddDays(-1), cancelAtPeriodEnd: true);
        Subscription notFlagged = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active,
            periodEnd: Now.AddDays(-1));
        await ((ISubscriptionWriter)_writer).AddAsync(pending, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(notFlagged, TestContext.Current.CancellationToken);

        IReadOnlyList<Subscription> result = await _reader.GetPendingCancelAtPeriodEndAsync(
            Now, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(pending.Id);
    }

    [Fact]
    public async Task GetActiveByPlanAsync_FiltersOnPlanAndStatus()
    {
        var planId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        Subscription onPlan = NewSubscription(tenantId, planId: planId, forceStatus: SubscriptionStatus.Active);
        Subscription otherPlan = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active);
        await ((ISubscriptionWriter)_writer).AddAsync(onPlan, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(otherPlan, TestContext.Current.CancellationToken);

        IReadOnlyList<Subscription> result = await _reader.GetActiveByPlanAsync(
            PlanId.Create(planId), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(onPlan.Id);
    }

    [Fact]
    public async Task GetPastDueForTenantAsync_ReturnsPastDueOnly()
    {
        var tenantId = Guid.NewGuid();
        Subscription active = NewSubscription(tenantId, forceStatus: SubscriptionStatus.Active);
        Subscription pastDue = NewSubscription(tenantId, forceStatus: SubscriptionStatus.PastDue);
        await ((ISubscriptionWriter)_writer).AddAsync(active, TestContext.Current.CancellationToken);
        await ((ISubscriptionWriter)_writer).AddAsync(pastDue, TestContext.Current.CancellationToken);

        Subscription? result = await _reader.GetPastDueForTenantAsync(
            tenantId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(pastDue.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStateTransition()
    {
        var tenantId = Guid.NewGuid();
        Subscription sub = NewSubscription(tenantId, trialEndsAt: Now.AddDays(7));
        await ((ISubscriptionWriter)_writer).AddAsync(sub, TestContext.Current.CancellationToken);

        sub.Activate();
        await ((ISubscriptionWriter)_writer).UpdateAsync(sub, TestContext.Current.CancellationToken);

        Subscription? loaded = await _reader.GetByIdAsync(
            SubscriptionId.Create(sub.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Status.ShouldBe(SubscriptionStatus.Active);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<SubscriptionsDbContext> options,
        ICurrentTenant currentTenant)
        : IDbContextFactory<SubscriptionsDbContext>
    {
        private readonly IDataFilter _filter = new DataFilter();

        public SubscriptionsDbContext CreateDbContext() =>
            new(options, currentTenant, _filter);

        public Task<SubscriptionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionsDbContext(options, currentTenant, _filter));
    }
}
