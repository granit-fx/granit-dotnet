using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Tests.MultiTenancy;

public sealed class AllowAnonymousTenantAttributeTests
{
    [Fact]
    public void CanBeInstantiated()
    {
        AllowAnonymousTenantAttribute attr = new();

        attr.ShouldNotBeNull();
    }

    [Fact]
    public void Attribute_TargetsClassAndMethod()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AllowAnonymousTenantAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Method);
    }

    [Fact]
    public void Attribute_DoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(AllowAnonymousTenantAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeFalse();
    }

    [Fact]
    public void IsAttribute()
    {
        AllowAnonymousTenantAttribute attr = new();

        attr.ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void CanBeAppliedToTestClass()
    {
        var attr = (AllowAnonymousTenantAttribute?)Attribute.GetCustomAttribute(
            typeof(DecoratedClass), typeof(AllowAnonymousTenantAttribute));

        attr.ShouldNotBeNull();
    }

    [AllowAnonymousTenant]
    private sealed class DecoratedClass;
}
