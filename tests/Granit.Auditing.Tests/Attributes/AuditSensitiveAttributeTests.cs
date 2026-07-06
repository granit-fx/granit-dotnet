using Granit.DataProtection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Attributes;

public sealed class SensitiveDataAttributeTests
{
    [Fact]
    public void CanBeAppliedToProperty()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(SensitiveDataAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }

    [Fact]
    public void CannotBeAppliedToClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(SensitiveDataAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage.ValidOn & AttributeTargets.Class).ShouldBe((AttributeTargets)0);
    }

    private sealed class EntityWithSensitive
    {
        [SensitiveData]
        public string Password { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void IsDetectedOnProperty()
    {
        System.Reflection.PropertyInfo? passwordProp = typeof(EntityWithSensitive).GetProperty("Password");
        System.Reflection.PropertyInfo? nameProp = typeof(EntityWithSensitive).GetProperty("Name");

        Attribute.IsDefined(passwordProp!, typeof(SensitiveDataAttribute)).ShouldBeTrue();
        Attribute.IsDefined(nameProp!, typeof(SensitiveDataAttribute)).ShouldBeFalse();
    }
}
