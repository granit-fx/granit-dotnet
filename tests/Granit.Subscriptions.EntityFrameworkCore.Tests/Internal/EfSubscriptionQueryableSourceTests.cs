using Granit.Contacts.Domain.ValueObjects;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Internal;

[Collection(SubscriptionsDbSerialGroup.Name)]
public sealed class EfSubscriptionQueryableSourceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly DbContextOptions<SubscriptionsDbContext> _options;

    public EfSubscriptionQueryableSourceTests()
    {
        _options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseInMemoryDatabase($"subscriptions-qsource-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    public void Dispose()
    {
        using var db = new SubscriptionsDbContext(_options);
        db.Database.EnsureDeleted();
    }

    private static Subscription NewSubscription(Guid tenantId)
    {
        var period = new SubscriptionPeriod(Now.AddMonths(-1), Now.AddMonths(1), BillingCycleAnchor: Now.AddMonths(-1));
        return Subscription.Create(
            Guid.NewGuid(), tenantId,
            ContactId.Create(Guid.NewGuid()),
            PlanId.Create(Guid.NewGuid()), "EUR", period);
    }

    [Fact]
    public void GetQueryable_ReturnsAsNoTracking()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        IDataFilter filter = new DataFilter();
        IDbContextFactory<SubscriptionsDbContext> factory = new TestFactory(_options, tenant, filter);

        using var source = new EfSubscriptionQueryableSource(factory, tenant, filter);

        source.GetQueryable().ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NoTenantContext_DisablesTenantFilter()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IDataFilter filter = Substitute.For<IDataFilter>();
        IDisposable bypass = Substitute.For<IDisposable>();
        filter.Disable<IMultiTenant>().Returns(bypass);
        IDbContextFactory<SubscriptionsDbContext> factory = new TestFactory(_options, tenant, filter);

        using (var source = new EfSubscriptionQueryableSource(factory, tenant, filter))
        {
            filter.Received(1).Disable<IMultiTenant>();
        }

        bypass.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_WithTenantContext_DoesNotDisableFilter()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        IDataFilter filter = Substitute.For<IDataFilter>();
        IDbContextFactory<SubscriptionsDbContext> factory = new TestFactory(_options, tenant, filter);

        using var source = new EfSubscriptionQueryableSource(factory, tenant, filter);

        filter.DidNotReceive().Disable<IMultiTenant>();
    }

    private sealed class TestFactory(
        DbContextOptions<SubscriptionsDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter filter)
        : IDbContextFactory<SubscriptionsDbContext>
    {
        public SubscriptionsDbContext CreateDbContext() => new(options, currentTenant, filter);

        public Task<SubscriptionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionsDbContext(options, currentTenant, filter));
    }
}
