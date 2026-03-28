using Shouldly;

namespace Granit.Mcp.Tests;

public sealed class McpTenantScopeAttributeTests
{
    [Fact]
    public void RequireTenant_DefaultsToFalse() =>
        new McpTenantScopeAttribute().RequireTenant.ShouldBeFalse();

    [Fact]
    public void RequireTenant_CanBeSetToTrue() =>
        new McpTenantScopeAttribute { RequireTenant = true }.RequireTenant.ShouldBeTrue();

    [Fact]
    public void Attribute_TargetsClass()
    {
        AttributeUsageAttribute usage = typeof(McpTenantScopeAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void Attribute_IsSealed() =>
        typeof(McpTenantScopeAttribute).IsSealed.ShouldBeTrue();
}
