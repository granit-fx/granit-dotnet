using Granit.Http.ApiVersioning.Deprecation;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiVersioning.Tests;

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
    public void SunsetDate_CanBeSet()
    {
        DeprecatedAttribute attribute = new() { SunsetDate = "2025-11-01" };

        attribute.SunsetDate.ShouldBe("2025-11-01");
    }

    [Fact]
    public void Link_CanBeSet()
    {
        DeprecatedAttribute attribute = new() { Link = "https://docs.example.com/migration" };

        attribute.Link.ShouldBe("https://docs.example.com/migration");
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
