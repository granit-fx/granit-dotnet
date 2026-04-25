using Granit.DataFiltering;
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
public sealed class EfPlanStoreTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory;
    private readonly EfPlanReader _reader;
    private readonly EfPlanWriter _writer;

    public EfPlanStoreTests()
    {
        DbContextOptions<SubscriptionsDbContext> options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseInMemoryDatabase($"subscriptions-plan-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestDbContextFactory(options);
        _reader = new EfPlanReader(_factory);
        _writer = new EfPlanWriter(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await using SubscriptionsDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static Plan NewPublishedPlan(string name = "Pro", decimal price = 25m, string currency = "EUR")
    {
        var plan = Plan.Create(Guid.NewGuid(), name, "desc", PricingModel.Flat, BillingInterval.Monthly);
        plan.AddPrice(PlanPrice.Create(Guid.NewGuid(), price, currency, BillingInterval.Monthly, Now));
        plan.Publish();
        return plan;
    }

    [Fact]
    public async Task GetByIdAsync_ExistingPlan_ReturnsWithPrices()
    {
        Plan plan = NewPublishedPlan();
        await ((IPlanWriter)_writer).AddAsync(plan, TestContext.Current.CancellationToken);

        Plan? result = await _reader.GetByIdAsync(PlanId.Create(plan.Id), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("Pro");
        result.Prices.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        Plan? result = await _reader.GetByIdAsync(
            PlanId.Create(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAvailablePlansAsync_ReturnsOnlyPublished()
    {
        var draft = Plan.Create(Guid.NewGuid(), "Draft", null, PricingModel.Flat, BillingInterval.Monthly);
        Plan published = NewPublishedPlan("Pub");

        await ((IPlanWriter)_writer).AddAsync(draft, TestContext.Current.CancellationToken);
        await ((IPlanWriter)_writer).AddAsync(published, TestContext.Current.CancellationToken);

        IReadOnlyList<Plan> result = await _reader.GetAvailablePlansAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Pub");
    }

    [Fact]
    public async Task GetByExternalIdAsync_MatchesProviderAndExternalId()
    {
        Plan plan = NewPublishedPlan();
        plan.AddExternalMapping(PlanExternalMapping.Create(Guid.NewGuid(), "stripe", "price_123"));
        await ((IPlanWriter)_writer).AddAsync(plan, TestContext.Current.CancellationToken);

        Plan? hit = await _reader.GetByExternalIdAsync(
            "stripe", "price_123", TestContext.Current.CancellationToken);
        Plan? miss = await _reader.GetByExternalIdAsync(
            "stripe", "missing", TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(plan.Id);
        miss.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        Plan plan = NewPublishedPlan();
        await ((IPlanWriter)_writer).AddAsync(plan, TestContext.Current.CancellationToken);

        plan.Archive();
        await ((IPlanWriter)_writer).UpdateAsync(plan, TestContext.Current.CancellationToken);

        Plan? loaded = await _reader.GetByIdAsync(PlanId.Create(plan.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.LifecycleStatus.ShouldBe(Granit.Workflow.Domain.WorkflowLifecycleStatus.Archived);
    }

    [Fact]
    public async Task UpdateAsync_NewPriceVersionInGraph_IsAddedNotErrored()
    {
        Plan plan = NewPublishedPlan();
        await ((IPlanWriter)_writer).AddAsync(plan, TestContext.Current.CancellationToken);

        var newVersion = PlanPrice.Create(Guid.NewGuid(), 30m, "EUR", BillingInterval.Monthly, Now.AddDays(1));
        plan.AddPriceVersion(newVersion, Now.AddDays(1));

        await Should.NotThrowAsync(
            () => ((IPlanWriter)_writer).UpdateAsync(plan, TestContext.Current.CancellationToken));

        Plan? loaded = await _reader.GetByIdAsync(PlanId.Create(plan.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Prices.Count.ShouldBe(2);
    }

    private sealed class TestDbContextFactory(DbContextOptions<SubscriptionsDbContext> options)
        : IDbContextFactory<SubscriptionsDbContext>
    {
        private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
        private readonly IDataFilter _filter = new DataFilter();

        public SubscriptionsDbContext CreateDbContext() =>
            new(options, _tenant, _filter);

        public Task<SubscriptionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionsDbContext(options, _tenant, _filter));
    }
}

[CollectionDefinition(SubscriptionsDbSerialGroup.Name, DisableParallelization = true)]
public sealed class SubscriptionsDbSerialGroup
{
    public const string Name = "Subscriptions-Db-serial";
}
