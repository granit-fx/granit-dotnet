using Granit.Http.ApiDocumentation.Deprecation;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests.Deprecation;

public sealed class DeprecatedAttributeTests
{
    [Fact]
    public void DefaultValues_SunsetDateIsNull()
    {
        DeprecatedAttribute attribute = new();

        attribute.SunsetDate.ShouldBeNull();
    }

    [Fact]
    public void DefaultValues_LinkIsNull()
    {
        DeprecatedAttribute attribute = new();

        attribute.Link.ShouldBeNull();
    }

    [Fact]
    public void SunsetDate_IsTypedDateOnly()
    {
        DeprecatedAttribute attribute = new() { SunsetDate = new DateOnly(2025, 11, 1) };

        attribute.SunsetDate.ShouldBe(new DateOnly(2025, 11, 1));
    }

    [Fact]
    public void AttributeUsage_AllowsMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(DeprecatedAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        (usage!.ValidOn & AttributeTargets.Method).ShouldBe(AttributeTargets.Method);
        (usage.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeFalse();
    }
}
