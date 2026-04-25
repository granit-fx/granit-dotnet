using Granit.Domain;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Metering.EntityFrameworkCore.Tests.Internal;

[Collection(MeteringDbSerialGroup.Name)]
public sealed class EfUsageAggregateStoreTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Hour0 = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TestFactory _factory;
    private readonly EfUsageAggregateStore _store;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EfUsageAggregateStoreTests()
    {
        DbContextOptions<MeteringDbContext> options = new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase($"metering-agg-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _store = new EfUsageAggregateStore(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await using MeteringDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task SeedAsync(UsageAggregate agg)
    {
        await using MeteringDbContext db = await _factory.CreateDbContextAsync();
        // IMultiTenant TenantId requires explicit cast: it's settable via the interface.
        ((IMultiTenant)agg).TenantId = _tenantId;
        db.UsageAggregates.Add(agg);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetForPeriodAsync_MatchingPeriod_Returns()
    {
        var meterId = Guid.NewGuid();
        var agg = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.Hourly,
            Hour0, Hour0.AddHours(1), aggregatedValue: 10m, eventCount: 5);
        await SeedAsync(agg);

        UsageAggregate? result = await _store.GetForPeriodAsync(
            _tenantId, MeterDefinitionId.Create(meterId),
            Hour0, Hour0.AddHours(1), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AggregatedValue.ShouldBe(10m);
    }

    [Fact]
    public async Task GetForPeriodAsync_NoMatch_ReturnsNull()
    {
        UsageAggregate? result = await _store.GetForPeriodAsync(
            _tenantId, MeterDefinitionId.Create(Guid.NewGuid()),
            Hour0, Hour0.AddHours(1), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsLatestBillingPeriodAggregate()
    {
        var meterId = Guid.NewGuid();
        var older = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.BillingPeriod,
            Hour0.AddDays(-30), Hour0, aggregatedValue: 100m, eventCount: 10);
        var newer = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.BillingPeriod,
            Hour0, Hour0.AddDays(30), aggregatedValue: 200m, eventCount: 20);
        await SeedAsync(older);
        await SeedAsync(newer);

        UsageAggregate? result = await _store.GetCurrentAsync(
            _tenantId, MeterDefinitionId.Create(meterId), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AggregatedValue.ShouldBe(200m);
    }

    [Fact]
    public async Task GetCurrentAsync_OnlyHourlyAggregatesExist_ReturnsNull()
    {
        var meterId = Guid.NewGuid();
        var hourly = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.Hourly,
            Hour0, Hour0.AddHours(1), aggregatedValue: 5m, eventCount: 1);
        await SeedAsync(hourly);

        UsageAggregate? result = await _store.GetCurrentAsync(
            _tenantId, MeterDefinitionId.Create(meterId), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllForPeriodAsync_FiltersWithinWindow()
    {
        var meterId = Guid.NewGuid();
        var inside = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.Hourly,
            Hour0.AddHours(1), Hour0.AddHours(2), 5m, 1);
        var outside = UsageAggregate.Create(
            Guid.NewGuid(), meterId, AggregationPeriod.Hourly,
            Hour0.AddDays(-2), Hour0.AddDays(-2).AddHours(1), 99m, 1);
        await SeedAsync(inside);
        await SeedAsync(outside);

        IReadOnlyList<UsageAggregate> result = await _store.GetAllForPeriodAsync(
            _tenantId, Hour0, Hour0.AddDays(1), TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].AggregatedValue.ShouldBe(5m);
    }

    /// <summary>
    /// Factory that omits both <see cref="ICurrentTenant"/> and <see cref="IDataFilter"/>:
    /// the multi-tenant query filter is then never registered (host context), and reads
    /// see all rows regardless of <see cref="IMultiTenant.TenantId"/>. This avoids
    /// flakiness from <see cref="DataFilter"/>'s static AsyncLocal state under parallel
    /// test execution.
    /// </summary>
    private sealed class TestFactory(DbContextOptions<MeteringDbContext> options)
        : IDbContextFactory<MeteringDbContext>
    {
        public MeteringDbContext CreateDbContext() => new(options);

        public Task<MeteringDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new MeteringDbContext(options));
    }
}

[CollectionDefinition(MeteringDbSerialGroup.Name, DisableParallelization = true)]
public sealed class MeteringDbSerialGroup
{
    public const string Name = "Metering-Db-serial";
}
