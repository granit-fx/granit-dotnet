using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultPeriodAdvancementServiceTests
{
    private readonly ISubscriptionReader _reader = Substitute.For<ISubscriptionReader>();
    private readonly ISubscriptionWriter _writer = Substitute.For<ISubscriptionWriter>();
    private readonly IPlanReader _planReader = Substitute.For<IPlanReader>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly ILogger<DefaultPeriodAdvancementService> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultPeriodAdvancementService>();
    private readonly DefaultPeriodAdvancementService _sut;

    public DefaultPeriodAdvancementServiceTests()
    {
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
        _currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());
        DateTimeOffset now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);
        _sut = new DefaultPeriodAdvancementService(
            _reader, _writer, _planReader, _clock, _currentTenant, _dataFilter, _logger);
    }

    private static Subscription CreateSubscriptionAtPeriodEnd(Guid tenantId, PlanId planId)
    {
        DateTimeOffset periodStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset periodEnd = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        return Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            planId,
            currency: "EUR",
            new SubscriptionPeriod(periodStart, periodEnd, BillingCycleAnchor: periodStart));
    }

    // ── AdvancePeriodsAsync tests ─────────────────────────────────

    [Fact]
    public async Task AdvancePeriodsAsync_NoSubscriptions_ShouldNotWrite()
    {
        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Subscription>());

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdvancePeriodsAsync_WithMonthlyPlan_ShouldAdvanceByOneMonth()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription sub = CreateSubscriptionAtPeriodEnd(tenantId, planId);

        var plan = Plan.Create(planId, "Pro", null, PricingModel.Flat, BillingInterval.Monthly);
        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub]);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        sub.CurrentPeriodStart.ShouldBe(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        sub.CurrentPeriodEnd.ShouldBe(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdvancePeriodsAsync_WithYearlyPlan_ShouldAdvanceByTwelveMonths()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription sub = CreateSubscriptionAtPeriodEnd(tenantId, planId);

        var plan = Plan.Create(planId, "Enterprise", null, PricingModel.Flat, BillingInterval.Yearly);
        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub]);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        sub.CurrentPeriodEnd.ShouldBe(new DateTimeOffset(2027, 4, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task AdvancePeriodsAsync_WithQuarterlyPlan_ShouldAdvanceByThreeMonths()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription sub = CreateSubscriptionAtPeriodEnd(tenantId, planId);

        var plan = Plan.Create(planId, "Business", null, PricingModel.Flat, BillingInterval.Quarterly);
        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub]);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        sub.CurrentPeriodEnd.ShouldBe(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        await _writer.Received(1).UpdateAsync(sub, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdvancePeriodsAsync_PlanNotFound_ShouldDefaultToMonthly()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription sub = CreateSubscriptionAtPeriodEnd(tenantId, planId);

        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub]);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns((Plan?)null);

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        sub.CurrentPeriodEnd.ShouldBe(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task AdvancePeriodsAsync_ExceptionOnOne_ShouldContinueWithOthers()
    {
        var planId1 = PlanId.Create(Guid.NewGuid());
        var planId2 = PlanId.Create(Guid.NewGuid());
        Subscription sub1 = CreateSubscriptionAtPeriodEnd(Guid.NewGuid(), planId1);
        Subscription sub2 = CreateSubscriptionAtPeriodEnd(Guid.NewGuid(), planId2);

        _reader.GetAtPeriodEndAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([sub1, sub2]);
        _planReader.GetByIdAsync(planId1, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Plan?>(new InvalidOperationException("DB error")));
        _planReader.GetByIdAsync(planId2, Arg.Any<CancellationToken>())
            .Returns((Plan?)null);

        await _sut.AdvancePeriodsAsync(TestContext.Current.CancellationToken);

        sub2.CurrentPeriodEnd.ShouldBe(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        await _writer.Received(1).UpdateAsync(sub2, Arg.Any<CancellationToken>());
    }

    // ── AdvanceByInterval tests ───────────────────────────────────

    [Fact]
    public void AdvanceByInterval_Monthly_ShouldAddOneMonth()
    {
        DateTimeOffset start = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = DefaultPeriodAdvancementService.AdvanceByInterval(start, BillingInterval.Monthly);

        result.ShouldBe(new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AdvanceByInterval_Quarterly_ShouldAddThreeMonths()
    {
        DateTimeOffset start = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = DefaultPeriodAdvancementService.AdvanceByInterval(start, BillingInterval.Quarterly);

        result.ShouldBe(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AdvanceByInterval_Yearly_ShouldAddTwelveMonths()
    {
        DateTimeOffset start = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = DefaultPeriodAdvancementService.AdvanceByInterval(start, BillingInterval.Yearly);

        result.ShouldBe(new DateTimeOffset(2027, 1, 15, 0, 0, 0, TimeSpan.Zero));
    }
}
