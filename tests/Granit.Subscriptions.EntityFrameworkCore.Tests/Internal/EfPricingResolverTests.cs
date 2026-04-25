using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.EntityFrameworkCore.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.EntityFrameworkCore.Tests.Internal;

public sealed class EfPricingResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IPlanReader _planReader = Substitute.For<IPlanReader>();
    private readonly EfPricingResolver _sut;

    public EfPricingResolverTests()
    {
        _sut = new EfPricingResolver(_planReader);
    }

    private static Plan PlanWithPrice(
        PricingModel model,
        decimal amount = 10m,
        string currency = "EUR",
        BillingInterval interval = BillingInterval.Monthly,
        Guid? priceId = null)
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", null, model, interval);
        var price = PlanPrice.Create(
            priceId ?? Guid.NewGuid(), amount, currency, interval, Now);
        plan.AddPrice(price);
        return plan;
    }

    // ── ResolveBasePriceAsync ─────────────────────────────────────

    [Fact]
    public async Task ResolveBasePriceAsync_PlanMissing_ReturnsZero()
    {
        var planId = PlanId.Create(Guid.NewGuid());
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns((Plan?)null);

        decimal result = await _sut.ResolveBasePriceAsync(
            planId, "EUR", BillingInterval.Monthly,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ResolveBasePriceAsync_DynamicResolution_ReturnsActivePriceAmount()
    {
        Plan plan = PlanWithPrice(PricingModel.Flat, amount: 25m);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveBasePriceAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(25m);
    }

    [Fact]
    public async Task ResolveBasePriceAsync_NoMatchingPrice_ReturnsZero()
    {
        Plan plan = PlanWithPrice(PricingModel.Flat, currency: "EUR", interval: BillingInterval.Monthly);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveBasePriceAsync(
            PlanId.Create(plan.Id), "USD", BillingInterval.Yearly,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ResolveBasePriceAsync_PinnedPriceId_ResolvesById()
    {
        var pinnedId = Guid.NewGuid();
        Plan plan = PlanWithPrice(PricingModel.Flat, amount: 99m, priceId: pinnedId);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveBasePriceAsync(
            PlanId.Create(plan.Id), "USD", BillingInterval.Yearly, planPriceId: pinnedId,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(99m);
    }

    [Fact]
    public async Task ResolveBasePriceAsync_PinnedPriceIdNotFound_ReturnsZero()
    {
        Plan plan = PlanWithPrice(PricingModel.Flat);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveBasePriceAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, planPriceId: Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    // ── ResolveUsageUnitPriceAsync ────────────────────────────────

    [Fact]
    public async Task ResolveUsageUnitPriceAsync_PlanMissing_ReturnsZero()
    {
        var planId = PlanId.Create(Guid.NewGuid());
        _planReader.GetByIdAsync(planId, Arg.Any<CancellationToken>()).Returns((Plan?)null);

        decimal result = await _sut.ResolveUsageUnitPriceAsync(
            planId, "EUR", BillingInterval.Monthly, "calls",
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Theory]
    [InlineData(PricingModel.Flat)]
    [InlineData(PricingModel.PerSeat)]
    public async Task ResolveUsageUnitPriceAsync_NonUsageModel_ReturnsZero(PricingModel model)
    {
        Plan plan = PlanWithPrice(model, amount: 50m);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageUnitPriceAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls",
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Theory]
    [InlineData(PricingModel.PerUnit)]
    [InlineData(PricingModel.Tiered)]
    public async Task ResolveUsageUnitPriceAsync_UsageModel_ReturnsUnitAmount(PricingModel model)
    {
        Plan plan = PlanWithPrice(model, amount: 0.05m);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageUnitPriceAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls",
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0.05m);
    }

    // ── ResolveUsageAmountAsync ───────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ResolveUsageAmountAsync_NonPositiveQuantity_ReturnsZero(decimal qty)
    {
        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(Guid.NewGuid()), "EUR", BillingInterval.Monthly, "calls", qty,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
        await _planReader.DidNotReceive().GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_PlanMissing_ReturnsZero()
    {
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns((Plan?)null);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(Guid.NewGuid()), "EUR", BillingInterval.Monthly, "calls", 100m,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_NonUsageModel_ReturnsZero()
    {
        Plan plan = PlanWithPrice(PricingModel.Flat);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls", 100m,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_NoMatchingPrice_ReturnsZero()
    {
        Plan plan = PlanWithPrice(PricingModel.PerUnit, currency: "EUR");
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "USD", BillingInterval.Monthly, "calls", 100m,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(0m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_PerUnitFlatPrice_ReturnsQuantityTimesAmount()
    {
        Plan plan = PlanWithPrice(PricingModel.PerUnit, amount: 0.10m);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls", 250m,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(25m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_TieredButNoTiers_ReturnsQuantityTimesAmount()
    {
        Plan plan = PlanWithPrice(PricingModel.Tiered, amount: 0.10m);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls", 100m,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(10m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_TieredVolume_AppliesHighestReachedTierToAll()
    {
        var plan = Plan.Create(Guid.NewGuid(), "Pro", null, PricingModel.Tiered, BillingInterval.Monthly);
        var priceId = Guid.NewGuid();
        var price = PlanPrice.Create(priceId, amount: 0m, "EUR", BillingInterval.Monthly, Now);
        price.SetTiers(TieringMode.Volume,
        [
            PricingTier.Create(Guid.NewGuid(), priceId, sortOrder: 0, upToQuantity: 10_000m, unitAmount: 0.10m),
            PricingTier.Create(Guid.NewGuid(), priceId, sortOrder: 1, upToQuantity: 100_000m, unitAmount: 0.05m),
            PricingTier.Create(Guid.NewGuid(), priceId, sortOrder: 2, upToQuantity: null,    unitAmount: 0.02m),
        ]);
        plan.AddPrice(price);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls", 50_000m,
            cancellationToken: TestContext.Current.CancellationToken);

        // Volume: 50_000 falls in second tier → 50_000 × 0.05 = 2500
        result.ShouldBe(2_500m);
    }

    [Fact]
    public async Task ResolveUsageAmountAsync_PinnedPriceId_ResolvesByIdAndApplies()
    {
        var pinnedId = Guid.NewGuid();
        Plan plan = PlanWithPrice(PricingModel.PerUnit, amount: 0.20m, priceId: pinnedId);
        _planReader.GetByIdAsync(Arg.Any<PlanId>(), Arg.Any<CancellationToken>()).Returns(plan);

        decimal result = await _sut.ResolveUsageAmountAsync(
            PlanId.Create(plan.Id), "EUR", BillingInterval.Monthly, "calls", quantity: 50m,
            planPriceId: pinnedId,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(10m);
    }
}
