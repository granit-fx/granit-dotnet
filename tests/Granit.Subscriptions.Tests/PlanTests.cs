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

        var price = PlanPrice.Create(Guid.NewGuid(), 29.99m, "EUR", BillingInterval.Monthly, DateTimeOffset.UtcNow);
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

    // ── Price versioning tests ────────────────────────────────────

    [Fact]
    public void AddPriceVersion_OnDraftPlan_ShouldAddPrice()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var price = PlanPrice.Create(Guid.NewGuid(), 29.99m, "EUR", BillingInterval.Monthly, now);

        PlanPrice? replaced = plan.AddPriceVersion(price, now);

        replaced.ShouldBeNull();
        plan.Prices.Count.ShouldBe(1);
        plan.Prices[0].Amount.ShouldBe(29.99m);
        plan.Prices[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public void AddPriceVersion_OnPublishedPlan_ShouldReplaceExisting()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var newPrice = PlanPrice.Create(Guid.NewGuid(), 39.99m, "EUR", BillingInterval.Monthly, now);
        PlanPrice? replaced = plan.AddPriceVersion(newPrice, now);

        replaced.ShouldNotBeNull();
        replaced!.Amount.ShouldBe(29.99m);
        replaced.IsActive.ShouldBeFalse();
        replaced.ReplacedByPriceId.ShouldBe(newPrice.Id);
        replaced.ReplacedAt.ShouldBe(now);

        plan.Prices.Count.ShouldBe(2);
        plan.GetActivePrice("EUR", BillingInterval.Monthly)!.Amount.ShouldBe(39.99m);
    }

    [Fact]
    public void AddPriceVersion_OnArchivedPlan_ShouldThrow()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);
        plan.Archive();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var newPrice = PlanPrice.Create(Guid.NewGuid(), 39.99m, "EUR", BillingInterval.Monthly, now);

        Should.Throw<InvalidOperationException>(() => plan.AddPriceVersion(newPrice, now));
    }

    [Fact]
    public void AddPriceVersion_DifferentCurrency_ShouldNotReplaceExisting()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var usdPrice = PlanPrice.Create(Guid.NewGuid(), 34.99m, "USD", BillingInterval.Monthly, now);
        PlanPrice? replaced = plan.AddPriceVersion(usdPrice, now);

        replaced.ShouldBeNull();
        plan.Prices.Count.ShouldBe(2);
        plan.GetActivePrice("EUR", BillingInterval.Monthly)!.Amount.ShouldBe(29.99m);
        plan.GetActivePrice("USD", BillingInterval.Monthly)!.Amount.ShouldBe(34.99m);
    }

    [Fact]
    public void GetPriceHistory_ShouldReturnNewestFirst()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var v2 = PlanPrice.Create(Guid.NewGuid(), 39.99m, "EUR", BillingInterval.Monthly, now);
        plan.AddPriceVersion(v2, now);

        DateTimeOffset later = now.AddDays(30);
        var v3 = PlanPrice.Create(Guid.NewGuid(), 49.99m, "EUR", BillingInterval.Monthly, later);
        plan.AddPriceVersion(v3, later);

        IReadOnlyList<PlanPrice> history = plan.GetPriceHistory("EUR", BillingInterval.Monthly);

        history.Count.ShouldBe(3);
        history[0].Amount.ShouldBe(49.99m);
        history[1].Amount.ShouldBe(39.99m);
        history[2].Amount.ShouldBe(29.99m);
    }

    [Fact]
    public void PlanPrice_MarkReplaced_Twice_ShouldThrow()
    {
        var price = PlanPrice.Create(Guid.NewGuid(), 29.99m, "EUR", BillingInterval.Monthly, DateTimeOffset.UtcNow);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        price.MarkReplaced(Guid.NewGuid(), now);

        Should.Throw<InvalidOperationException>(() => price.MarkReplaced(Guid.NewGuid(), now));
    }

    // ======== AddFeatureValue ========

    [Fact]
    public void AddFeatureValue_WhenDraft_ShouldAddToCollection()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        var feature = PlanFeatureValue.Create(Guid.NewGuid(), "MaxUsers", "100");
        plan.AddFeatureValue(feature);

        plan.PlanFeatureValues.Count.ShouldBe(1);
        plan.PlanFeatureValues[0].FeatureName.ShouldBe("MaxUsers");
    }

    [Fact]
    public void AddFeatureValue_WithNull_ShouldThrow()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        Should.Throw<ArgumentNullException>(() => plan.AddFeatureValue(null!));
    }

    [Fact]
    public void AddFeatureValue_WhenPublished_ShouldThrow()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);

        var feature = PlanFeatureValue.Create(Guid.NewGuid(), "MaxUsers", "100");

        Should.Throw<InvalidOperationException>(() => plan.AddFeatureValue(feature));
    }

    [Fact]
    public void AddFeatureValue_WhenArchived_ShouldThrow()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);
        plan.Archive();

        var feature = PlanFeatureValue.Create(Guid.NewGuid(), "MaxUsers", "100");

        Should.Throw<InvalidOperationException>(() => plan.AddFeatureValue(feature));
    }

    // ======== AddExternalMapping ========

    [Fact]
    public void AddExternalMapping_ShouldAddToCollection()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        var mapping = PlanExternalMapping.Create(Guid.NewGuid(), "stripe", "price_123");
        plan.AddExternalMapping(mapping);

        plan.ExternalMappings.Count.ShouldBe(1);
        plan.ExternalMappings[0].ProviderName.ShouldBe("stripe");
    }

    [Fact]
    public void AddExternalMapping_WithNull_ShouldThrow()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        Should.Throw<ArgumentNullException>(() => plan.AddExternalMapping(null!));
    }

    // ======== Publish guards ========

    [Fact]
    public void Publish_WithoutPrices_ShouldThrow()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        Should.Throw<InvalidOperationException>(() => plan.Publish());
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ShouldThrow()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);

        Should.Throw<InvalidOperationException>(() => plan.Publish());
    }

    // ======== Archive guards ========

    [Fact]
    public void Archive_WhenDraft_ShouldThrow()
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, BillingInterval.Monthly);

        Should.Throw<InvalidOperationException>(() => plan.Archive());
    }

    // ======== Update guards ========

    [Fact]
    public void Update_WhenPublished_ShouldThrow()
    {
        Plan plan = CreatePublishedPlan("EUR", BillingInterval.Monthly, 29.99m);

        Should.Throw<InvalidOperationException>(() => plan.Update("New Name", null, 1));
    }

    private static Plan CreatePublishedPlan(string currency, BillingInterval interval, decimal amount)
    {
        var plan = Plan.Create(
            Guid.NewGuid(), "Pro", null,
            PricingModel.Flat, interval);

        var price = PlanPrice.Create(Guid.NewGuid(), amount, currency, interval, DateTimeOffset.UtcNow.AddDays(-30));
        plan.AddPrice(price);
        plan.Publish();
        return plan;
    }
}
