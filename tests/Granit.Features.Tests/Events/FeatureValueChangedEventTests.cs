using Granit.Features.Events;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.Events;

public sealed class FeatureValueChangedEventTests
{
    [Fact]
    public void Constructor_WithTenantId_SetsProperties()
    {
        var tenantId = Guid.NewGuid();
        FeatureValueChangedEvent @event = new("App.VideoConsultation", tenantId);

        @event.FeatureName.ShouldBe("App.VideoConsultation");
        @event.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Constructor_WithNullTenantId_SetsNullTenantId()
    {
        FeatureValueChangedEvent @event = new("App.Feature", TenantId: null);

        @event.FeatureName.ShouldBe("App.Feature");
        @event.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Record_Equality_DifferentFeatureName_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        FeatureValueChangedEvent a = new("App.FeatureA", tenantId);
        FeatureValueChangedEvent b = new("App.FeatureB", tenantId);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_Equality_DifferentTenantId_AreNotEqual()
    {
        FeatureValueChangedEvent a = new("App.Feature", Guid.NewGuid());
        FeatureValueChangedEvent b = new("App.Feature", Guid.NewGuid());

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_Equality_NullVsNonNullTenantId_AreNotEqual()
    {
        FeatureValueChangedEvent a = new("App.Feature", null);
        FeatureValueChangedEvent b = new("App.Feature", Guid.NewGuid());

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_With_CreatesNewInstance()
    {
        var tenantId = Guid.NewGuid();
        FeatureValueChangedEvent original = new("App.Feature", tenantId);
        FeatureValueChangedEvent modified = original with { FeatureName = "App.Other" };

        modified.FeatureName.ShouldBe("App.Other");
        modified.TenantId.ShouldBe(tenantId);
        original.FeatureName.ShouldBe("App.Feature", "original must not be mutated");
    }

    [Fact]
    public void ToString_ContainsFeatureNameAndTenantId()
    {
        var tenantId = Guid.NewGuid();
        FeatureValueChangedEvent @event = new("App.Feature", tenantId);

        string text = @event.ToString();

        text.ShouldContain("App.Feature");
        text.ShouldContain(tenantId.ToString());
    }
}
