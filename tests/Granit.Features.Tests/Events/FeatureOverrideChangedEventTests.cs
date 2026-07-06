using Granit.Features.Events;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.Events;

public sealed class FeatureOverrideChangedEventTests
{
    // -------------------------------------------------------------------------
    // Constructor — all properties set
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        FeatureOverrideChangedEvent @event = new(
            "Acme.MaxUsers", tenantId, "50", "200", timestamp);

        @event.FeatureName.ShouldBe("Acme.MaxUsers");
        @event.TenantId.ShouldBe(tenantId);
        @event.OldValue.ShouldBe("50");
        @event.NewValue.ShouldBe("200");
        @event.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void Constructor_NullTenantId_IsAllowed()
    {
        FeatureOverrideChangedEvent @event = new(
            "Acme.Feature", null, null, "true", DateTimeOffset.UtcNow);

        @event.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NullOldValue_RepresentsCreation()
    {
        FeatureOverrideChangedEvent @event = new(
            "Acme.Feature", Guid.NewGuid(), null, "true", DateTimeOffset.UtcNow);

        @event.OldValue.ShouldBeNull();
        @event.NewValue.ShouldBe("true");
    }

    [Fact]
    public void Constructor_NullNewValue_RepresentsDeletion()
    {
        FeatureOverrideChangedEvent @event = new(
            "Acme.Feature", Guid.NewGuid(), "true", null, DateTimeOffset.UtcNow);

        @event.OldValue.ShouldBe("true");
        @event.NewValue.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Record equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Record_Equality_DifferentFeatureName_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        FeatureOverrideChangedEvent a = new("Acme.FeatureA", tenantId, "old", "new", timestamp);
        FeatureOverrideChangedEvent b = new("Acme.FeatureB", tenantId, "old", "new", timestamp);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_Equality_DifferentOldValue_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        FeatureOverrideChangedEvent a = new("Acme.Feature", tenantId, "50", "100", timestamp);
        FeatureOverrideChangedEvent b = new("Acme.Feature", tenantId, "60", "100", timestamp);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_With_CreatesNewInstance()
    {
        var tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        FeatureOverrideChangedEvent original = new("Acme.Feature", tenantId, "old", "new", timestamp);
        FeatureOverrideChangedEvent modified = original with { NewValue = "updated" };

        modified.NewValue.ShouldBe("updated");
        original.NewValue.ShouldBe("new", "original must not be mutated");
    }

    [Fact]
    public void ToString_ContainsFeatureNameAndValues()
    {
        var tenantId = Guid.NewGuid();
        FeatureOverrideChangedEvent @event = new(
            "Acme.Feature", tenantId, "old", "new", DateTimeOffset.UtcNow);

        string text = @event.ToString();

        text.ShouldContain("Acme.Feature");
        text.ShouldContain(tenantId.ToString());
    }
}
