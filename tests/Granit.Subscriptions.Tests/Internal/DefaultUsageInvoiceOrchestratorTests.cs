using Granit.Commands;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Internal;

public sealed class DefaultUsageInvoiceOrchestratorTests
{
    private readonly ISubscriptionReader _subscriptionReader = Substitute.For<ISubscriptionReader>();
    private readonly IPlanReader _planReader = Substitute.For<IPlanReader>();
    private readonly IPricingResolver _pricingResolver = Substitute.For<IPricingResolver>();
    private readonly ICommandSender _commandSender = Substitute.For<ICommandSender>();
    private readonly ILogger<DefaultUsageInvoiceOrchestrator> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultUsageInvoiceOrchestrator>();
    private readonly DefaultUsageInvoiceOrchestrator _sut;

    private static readonly DateTimeOffset PeriodStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    public DefaultUsageInvoiceOrchestratorTests()
    {
        _sut = new DefaultUsageInvoiceOrchestrator(
            _subscriptionReader, _planReader, _pricingResolver, _commandSender, _logger);
    }

    private static Subscription CreateActiveSubscription(Guid tenantId, PlanId planId, Guid? planPriceId = null)
    {
        return Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            planId,
            currency: "EUR",
            new SubscriptionPeriod(PeriodStart, PeriodEnd, BillingCycleAnchor: PeriodStart),
            planPriceId: planPriceId);
    }

    private static CreateUsageInvoiceRequest CreateValidRequest(
        Guid? tenantId = null,
        Guid? meterDefinitionId = null,
        decimal aggregatedValue = 150m)
    {
        return new CreateUsageInvoiceRequest(
            TenantId: tenantId ?? Guid.NewGuid(),
            MeterDefinitionId: meterDefinitionId ?? Guid.NewGuid(),
            MeterName: "API Calls",
            AggregatedValue: aggregatedValue,
            Unit: "calls",
            PeriodStart: PeriodStart,
            PeriodEnd: PeriodEnd);
    }

    // ======== Validation — empty TenantId ========

    [Fact]
    public async Task CreateInvoiceAsync_EmptyTenantId_ShouldReturnWithoutPublishing()
    {
        var request = new CreateUsageInvoiceRequest(
            TenantId: Guid.Empty,
            MeterDefinitionId: Guid.NewGuid(),
            MeterName: "API Calls",
            AggregatedValue: 100m,
            Unit: "calls",
            PeriodStart: PeriodStart,
            PeriodEnd: PeriodEnd);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Validation — PeriodEnd <= PeriodStart ========

    [Fact]
    public async Task CreateInvoiceAsync_PeriodEndBeforePeriodStart_ShouldReturnWithoutPublishing()
    {
        var request = new CreateUsageInvoiceRequest(
            TenantId: Guid.NewGuid(),
            MeterDefinitionId: Guid.NewGuid(),
            MeterName: "API Calls",
            AggregatedValue: 100m,
            Unit: "calls",
            PeriodStart: PeriodEnd,
            PeriodEnd: PeriodStart);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateInvoiceAsync_PeriodEndEqualsPeriodStart_ShouldReturnWithoutPublishing()
    {
        var request = new CreateUsageInvoiceRequest(
            TenantId: Guid.NewGuid(),
            MeterDefinitionId: Guid.NewGuid(),
            MeterName: "API Calls",
            AggregatedValue: 100m,
            Unit: "calls",
            PeriodStart: PeriodStart,
            PeriodEnd: PeriodStart);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== No active subscription ========

    [Fact]
    public async Task CreateInvoiceAsync_NoActiveSubscription_ShouldReturnWithoutPublishing()
    {
        var tenantId = Guid.NewGuid();
        CreateUsageInvoiceRequest request = CreateValidRequest(tenantId: tenantId);
        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Plan not found ========

    [Fact]
    public async Task CreateInvoiceAsync_PlanNotFound_ShouldReturnWithoutPublishing()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        Subscription subscription = CreateActiveSubscription(tenantId, planId);
        CreateUsageInvoiceRequest request = CreateValidRequest(tenantId: tenantId);

        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns((Plan?)null);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Usage only (base price = 0) ========

    [Fact]
    public async Task CreateInvoiceAsync_ZeroBasePrice_ShouldPublishUsageLineItemOnly()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var meterDefId = Guid.NewGuid();
        Subscription subscription = CreateActiveSubscription(tenantId, planId);
        var plan = Plan.Create(planId, "Usage", null, PricingModel.PerUnit, BillingInterval.Monthly);
        CreateUsageInvoiceRequest request = CreateValidRequest(
            tenantId: tenantId, meterDefinitionId: meterDefId, aggregatedValue: 500m);

        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(0m);
        _pricingResolver.ResolveUsageAmountAsync(
                planId, "EUR", BillingInterval.Monthly, meterDefId.ToString(), 500m, null, Arg.Any<CancellationToken>())
            .Returns(500m * 0.05m);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.Received(1).SendAsync(
            Arg.Is<CreateInvoiceCommand>(cmd =>
                cmd.TenantId == tenantId &&
                cmd.Currency == "EUR" &&
                cmd.LineItems.Count == 1 &&
                cmd.LineItems[0].SourceType == InvoiceSourceType.Usage &&
                cmd.LineItems[0].Quantity == 500m &&
                cmd.LineItems[0].UnitPrice == 0.05m),
            Arg.Any<CancellationToken>());
    }

    // ======== Flat + usage (base price > 0) ========

    [Fact]
    public async Task CreateInvoiceAsync_PositiveBasePrice_ShouldPublishFlatAndUsageLineItems()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var meterDefId = Guid.NewGuid();
        Subscription subscription = CreateActiveSubscription(tenantId, planId);
        var plan = Plan.Create(planId, "Pro Usage", null, PricingModel.PerUnit, BillingInterval.Monthly);
        CreateUsageInvoiceRequest request = CreateValidRequest(
            tenantId: tenantId, meterDefinitionId: meterDefId, aggregatedValue: 200m);

        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(49.99m);
        _pricingResolver.ResolveUsageAmountAsync(
                planId, "EUR", BillingInterval.Monthly, meterDefId.ToString(), 200m, null, Arg.Any<CancellationToken>())
            .Returns(200m * 0.10m);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.Received(1).SendAsync(
            Arg.Is<CreateInvoiceCommand>(cmd =>
                cmd.TenantId == tenantId &&
                cmd.LineItems.Count == 2 &&
                cmd.LineItems[0].SourceType == InvoiceSourceType.Subscription &&
                cmd.LineItems[0].Quantity == 1 &&
                cmd.LineItems[0].UnitPrice == 49.99m &&
                cmd.LineItems[1].SourceType == InvoiceSourceType.Usage &&
                cmd.LineItems[1].Quantity == 200m &&
                cmd.LineItems[1].UnitPrice == 0.10m &&
                cmd.BillingReason == BillingReason.SubscriptionCycle &&
                cmd.PeriodStart == PeriodStart &&
                cmd.PeriodEnd == PeriodEnd),
            Arg.Any<CancellationToken>());
    }

    // ======== Line item description format ========

    [Fact]
    public async Task CreateInvoiceAsync_UsageLineItem_ShouldIncludeMeterNameAndValue()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var meterDefId = Guid.NewGuid();
        Subscription subscription = CreateActiveSubscription(tenantId, planId);
        var plan = Plan.Create(planId, "Metered", null, PricingModel.PerUnit, BillingInterval.Monthly);
        var request = new CreateUsageInvoiceRequest(
            TenantId: tenantId,
            MeterDefinitionId: meterDefId,
            MeterName: "Storage",
            AggregatedValue: 75m,
            Unit: "GB",
            PeriodStart: PeriodStart,
            PeriodEnd: PeriodEnd);

        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(0m);
        _pricingResolver.ResolveUsageAmountAsync(
                planId, "EUR", BillingInterval.Monthly, meterDefId.ToString(), 75m, null, Arg.Any<CancellationToken>())
            .Returns(75m * 0.25m);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _commandSender.Received(1).SendAsync(
            Arg.Is<CreateInvoiceCommand>(cmd =>
                cmd.LineItems[0].Description == "Storage: 75 GB"),
            Arg.Any<CancellationToken>());
    }

    // ======== Pinned price passed to resolver ========

    [Fact]
    public async Task CreateInvoiceAsync_WithPinnedPrice_ShouldPassPlanPriceIdToResolvers()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var planPriceId = Guid.NewGuid();
        var meterDefId = Guid.NewGuid();
        Subscription subscription = CreateActiveSubscription(tenantId, planId, planPriceId);
        var plan = Plan.Create(planId, "Pro", null, PricingModel.PerUnit, BillingInterval.Monthly);
        CreateUsageInvoiceRequest request = CreateValidRequest(
            tenantId: tenantId, meterDefinitionId: meterDefId);

        _subscriptionReader.GetActiveForTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, planPriceId, Arg.Any<CancellationToken>())
            .Returns(29.99m);
        _pricingResolver.ResolveUsageAmountAsync(
                planId, "EUR", BillingInterval.Monthly, meterDefId.ToString(), 150m, planPriceId, Arg.Any<CancellationToken>())
            .Returns(150m * 0.01m);

        await _sut.CreateInvoiceAsync(request, TestContext.Current.CancellationToken);

        await _pricingResolver.Received(1).ResolveBasePriceAsync(
            planId, "EUR", BillingInterval.Monthly, planPriceId, Arg.Any<CancellationToken>());
        await _pricingResolver.Received(1).ResolveUsageAmountAsync(
            planId, "EUR", BillingInterval.Monthly, meterDefId.ToString(), 150m, planPriceId, Arg.Any<CancellationToken>());
    }
}
