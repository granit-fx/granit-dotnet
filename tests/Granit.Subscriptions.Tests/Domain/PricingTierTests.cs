using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests.Domain;

public sealed class PricingTierTests
{
    [Fact]
    public void Create_WithAllFields_StoresThem()
    {
        var id = Guid.NewGuid();
        var planPriceId = Guid.NewGuid();

        var tier = PricingTier.Create(id, planPriceId, sortOrder: 2,
            upToQuantity: 100m, unitAmount: 0.05m, flatAmount: 9.99m);

        tier.Id.ShouldBe(id);
        tier.PlanPriceId.ShouldBe(planPriceId);
        tier.SortOrder.ShouldBe(2);
        tier.UpToQuantity.ShouldBe(100m);
        tier.UnitAmount.ShouldBe(0.05m);
        tier.FlatAmount.ShouldBe(9.99m);
    }

    [Fact]
    public void Create_WithNullUpToQuantity_RepresentsOpenEndedTier()
    {
        var tier = PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), 0,
            upToQuantity: null, unitAmount: 0.01m);

        tier.UpToQuantity.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNegativeSortOrder_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), -1, 100m, 0.10m));

    [Fact]
    public void Create_WithNegativeUnitAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 100m, -0.01m));

    [Fact]
    public void Create_WithZeroUpToQuantity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0m, 0.10m));

    [Fact]
    public void Create_WithNegativeFlatAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            PricingTier.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 100m, 0.10m, flatAmount: -1m));
}
