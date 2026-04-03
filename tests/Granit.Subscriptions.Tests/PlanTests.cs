using Granit.Subscriptions.Domain;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Tests;

public sealed class PlanTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", "Professional plan",
            PricingModel.Flat, BillingInterval.Monthly);

        plan.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
        plan.Name.ShouldBe("Pro");
        plan.PricingModel.ShouldBe(PricingModel.Flat);
    }

    [Fact]
    public void Create_WithTrialDays_ShouldStoreValue()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Starter", null,
            PricingModel.PerSeat, BillingInterval.Yearly, trialDays: 14);

        plan.TrialDays.ShouldBe(14);
    }

    [Fact]
    public void Create_WithNullName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Plan.Create(
            Guid.NewGuid(), null!, null,
            PricingModel.Flat, BillingInterval.Monthly));
    }

    [Fact]
    public void Update_WhenDraft_ShouldSucceed()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Old", null,
            PricingModel.Flat, BillingInterval.Monthly);

        plan.Update("New", "Updated description", 5);

        plan.Name.ShouldBe("New");
        plan.Description.ShouldBe("Updated description");
        plan.SortOrder.ShouldBe(5);
    }

    [Fact]
    public void AddPrice_WhenDraft_ShouldAddToCollection()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        var price = PlanPrice.Create(Guid.NewGuid(), 29.99m, "EUR", BillingInterval.Monthly);
        plan.AddPrice(price);

        plan.Prices.Count.ShouldBe(1);
        plan.Prices[0].Amount.ShouldBe(29.99m);
    }

    [Fact]
    public void Prices_ShouldReturnReadOnlyList()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        plan.Prices.ShouldBeAssignableTo<IReadOnlyList<PlanPrice>>();
    }
}
