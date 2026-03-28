using Granit.Mcp.Options;
using Shouldly;

namespace Granit.Mcp.Tests.Options;

public sealed class McpToolDiscoveryModeTests
{
    [Fact]
    public void Explicit_HasValueZero() =>
        ((int)McpToolDiscoveryMode.Explicit).ShouldBe(0);

    [Fact]
    public void Auto_HasValueOne() =>
        ((int)McpToolDiscoveryMode.Auto).ShouldBe(1);

    [Fact]
    public void Enum_HasTwoValues() =>
        Enum.GetValues<McpToolDiscoveryMode>().Length.ShouldBe(2);
}
