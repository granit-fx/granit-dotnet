using Granit.Commands;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Parties.Domain.ValueObjects;
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

public sealed class DefaultBillingCycleInvoiceOrchestratorTests
{
    private readonly ISubscriptionReader _subscriptionReader = Substitute.For<ISubscriptionReader>();
    private readonly IPlanReader _planReader = Substitute.For<IPlanReader>();
    private readonly IPricingResolver _pricingResolver = Substitute.For<IPricingResolver>();
    private readonly ICommandSender _commandSender = Substitute.For<ICommandSender>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILogger<DefaultBillingCycleInvoiceOrchestrator> _logger =
        NullLoggerFactory.Instance.CreateLogger<DefaultBillingCycleInvoiceOrchestrator>();
    private readonly DefaultBillingCycleInvoiceOrchestrator _sut;

    private static readonly DateTimeOffset PeriodStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    public DefaultBillingCycleInvoiceOrchestratorTests()
    {
        _clock.Now.Returns(PeriodStart);
        _sut = new DefaultBillingCycleInvoiceOrchestrator(
            _subscriptionReader, _planReader, _pricingResolver, _commandSender, _clock, _logger);
    }

    private static Subscription CreateSubscription(Guid tenantId, PlanId planId, Guid? planPriceId = null)
    {
        return Subscription.Create(
            Guid.NewGuid(),
            tenantId,
            PartyId.Create(Guid.NewGuid()),
            planId,
            currency: "EUR",
            new SubscriptionPeriod(PeriodStart, PeriodEnd, BillingCycleAnchor: PeriodStart),
            planPriceId: planPriceId);
    }

    // ======== PlanNotFound ========

    [Fact]
    public async Task CreateInvoiceAsync_PlanNotFound_ShouldReturnWithoutPublishing()
    {
        var planId = PlanId.Create(Guid.NewGuid());
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns((Plan?)null);

        await _sut.CreateInvoiceAsync(
            Guid.NewGuid(), Guid.NewGuid(), planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Usage-based plans skipped ========

    [Theory]
    [InlineData(PricingModel.PerUnit)]
    [InlineData(PricingModel.Tiered)]
    public async Task CreateInvoiceAsync_UsageBasedPlan_ShouldSkip(PricingModel pricingModel)
    {
        var planId = PlanId.Create(Guid.NewGuid());
        var plan = Plan.Create(planId, "Usage Plan", null, pricingModel, BillingInterval.Monthly);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);

        await _sut.CreateInvoiceAsync(
            Guid.NewGuid(), Guid.NewGuid(), planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _subscriptionReader.DidNotReceive()
            .GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>());
        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== SubscriptionNotFound ========

    [Fact]
    public async Task CreateInvoiceAsync_SubscriptionNotFound_ShouldReturnWithoutPublishing()
    {
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Pro", null, PricingModel.Flat, BillingInterval.Monthly);
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>())
            .Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await _sut.CreateInvoiceAsync(
            subscriptionId, Guid.NewGuid(), planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Zero base price ========

    [Fact]
    public async Task CreateInvoiceAsync_ZeroBasePrice_ShouldReturnWithoutPublishing()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Free", null, PricingModel.Flat, BillingInterval.Monthly);
        Subscription subscription = CreateSubscription(tenantId, planId);

        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(0m);

        await _sut.CreateInvoiceAsync(
            subscriptionId, tenantId, planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Flat pricing ========

    [Fact]
    public async Task CreateInvoiceAsync_FlatPlan_ShouldPublishInvoiceWithSingleLineItem()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Pro", null, PricingModel.Flat, BillingInterval.Monthly);
        Subscription subscription = CreateSubscription(tenantId, planId);

        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(29.99m);

        await _sut.CreateInvoiceAsync(
            subscriptionId, tenantId, planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.Received(1).SendAsync(
            Arg.Is<CreateInvoiceCommand>(cmd =>
                cmd.TenantId == tenantId &&
                cmd.Currency == "EUR" &&
                cmd.CollectionMethod == CollectionMethod.Auto &&
                cmd.BillingReason == BillingReason.SubscriptionCycle &&
                cmd.LineItems.Count == 1 &&
                cmd.LineItems[0].Quantity == 1 &&
                cmd.LineItems[0].UnitPrice == 29.99m &&
                cmd.LineItems[0].SourceType == InvoiceSourceType.Subscription &&
                cmd.PeriodStart == PeriodStart &&
                cmd.PeriodEnd == PeriodEnd),
            Arg.Any<CancellationToken>());
    }

    // ======== PerSeat pricing ========

    [Fact]
    public async Task CreateInvoiceAsync_PerSeatPlan_ShouldPublishInvoiceWithSeatCountAsQuantity()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Team", null, PricingModel.PerSeat, BillingInterval.Monthly);
        Subscription subscription = CreateSubscription(tenantId, planId);
        subscription.AssignSeat(SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));
        subscription.AssignSeat(SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));
        subscription.AssignSeat(SubscriptionSeat.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));

        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(10m);

        await _sut.CreateInvoiceAsync(
            subscriptionId, tenantId, planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.Received(1).SendAsync(
            Arg.Is<CreateInvoiceCommand>(cmd =>
                cmd.LineItems.Count == 1 &&
                cmd.LineItems[0].Quantity == 3 &&
                cmd.LineItems[0].UnitPrice == 10m &&
                cmd.LineItems[0].Description.Contains("3 seat(s)")),
            Arg.Any<CancellationToken>());
    }

    // ======== Negative base price ========

    [Fact]
    public async Task CreateInvoiceAsync_NegativeBasePrice_ShouldReturnWithoutPublishing()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Promo", null, PricingModel.Flat, BillingInterval.Monthly);
        Subscription subscription = CreateSubscription(tenantId, planId);

        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, null, Arg.Any<CancellationToken>())
            .Returns(-5m);

        await _sut.CreateInvoiceAsync(
            subscriptionId, tenantId, planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _commandSender.DidNotReceive()
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    // ======== Pinned price (planPriceId) ========

    [Fact]
    public async Task CreateInvoiceAsync_WithPinnedPrice_ShouldPassPlanPriceIdToResolver()
    {
        var tenantId = Guid.NewGuid();
        var planId = PlanId.Create(Guid.NewGuid());
        var subscriptionId = Guid.NewGuid();
        var planPriceId = Guid.NewGuid();
        var plan = Plan.Create(planId, "Pro", null, PricingModel.Flat, BillingInterval.Monthly);
        Subscription subscription = CreateSubscription(tenantId, planId, planPriceId);

        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns(plan);
        _subscriptionReader.GetByIdAsync(subscriptionId, Arg.Any<CancellationToken>()).Returns(subscription);
        _pricingResolver.ResolveBasePriceAsync(
                planId, "EUR", BillingInterval.Monthly, planPriceId, Arg.Any<CancellationToken>())
            .Returns(19.99m);

        await _sut.CreateInvoiceAsync(
            subscriptionId, tenantId, planId, PeriodStart, PeriodEnd,
            TestContext.Current.CancellationToken);

        await _pricingResolver.Received(1).ResolveBasePriceAsync(
            planId, "EUR", BillingInterval.Monthly, planPriceId, Arg.Any<CancellationToken>());
        await _commandSender.Received(1)
            .SendAsync(Arg.Any<CreateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }
}
