using Granit.AuditLog.Attributes;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Attributes;

public sealed class AuditIgnoreAttributeTests
{
    [Fact]
    public void CanBeAppliedToClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AuditIgnoreAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void CanBeAppliedToProperty()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AuditIgnoreAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void CanBeInstantiated()
    {
        AuditIgnoreAttribute attribute = new();
        attribute.ShouldNotBeNull();
    }

    [AuditIgnore]
    private sealed class IgnoredEntity;

    [Fact]
    public void IsDetectedOnClass()
    {
        bool hasAttribute = Attribute.IsDefined(typeof(IgnoredEntity), typeof(AuditIgnoreAttribute));
        hasAttribute.ShouldBeTrue();
    }
}
