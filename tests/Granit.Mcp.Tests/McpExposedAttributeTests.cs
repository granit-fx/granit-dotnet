using Shouldly;

namespace Granit.Mcp.Tests;

public sealed class McpExposedAttributeTests
{
    [Fact]
    public void Attribute_TargetsClass()
    {
        AttributeUsageAttribute usage = typeof(McpExposedAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void Attribute_IsNotInherited()
    {
        AttributeUsageAttribute usage = typeof(McpExposedAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.Inherited.ShouldBeFalse();
    }

    [Fact]
    public void Attribute_IsSealed() =>
        typeof(McpExposedAttribute).IsSealed.ShouldBeTrue();
}
