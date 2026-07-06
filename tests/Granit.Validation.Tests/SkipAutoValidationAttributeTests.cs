using Granit.Validation.AspNetCore;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class SkipAutoValidationAttributeTests
{

    [Fact]
    public void IsAssignableFromAttribute()
    {
        SkipAutoValidationAttribute attribute = new();

        attribute.ShouldBeAssignableTo<Attribute>();
    }

    [Fact]
    public void AttributeUsage_TargetsMethodOnly()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(SkipAutoValidationAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeFalse();
    }
}
