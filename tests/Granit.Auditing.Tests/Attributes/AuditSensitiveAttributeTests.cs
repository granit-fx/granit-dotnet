using Granit.Auditing.Attributes;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Attributes;

public sealed class AuditSensitiveAttributeTests
{
    [Fact]
    public void CanBeAppliedToProperty()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AuditSensitiveAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void CannotBeAppliedToClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AuditSensitiveAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Class).ShouldBe((AttributeTargets)0);
    }

    [Fact]
    public void CanBeInstantiated()
    {
        AuditSensitiveAttribute attribute = new();
        attribute.ShouldNotBeNull();
    }

    private sealed class EntityWithSensitive
    {
        [AuditSensitive]
        public string Password { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void IsDetectedOnProperty()
    {
        System.Reflection.PropertyInfo? passwordProp = typeof(EntityWithSensitive).GetProperty("Password");
        System.Reflection.PropertyInfo? nameProp = typeof(EntityWithSensitive).GetProperty("Name");

        Attribute.IsDefined(passwordProp!, typeof(AuditSensitiveAttribute)).ShouldBeTrue();
        Attribute.IsDefined(nameProp!, typeof(AuditSensitiveAttribute)).ShouldBeFalse();
    }
}
