using Granit.Mcp.Options;
using Shouldly;

namespace Granit.Mcp.Tests.Options;

public sealed class GranitMcpOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        GranitMcpOptions.SectionName.ShouldBe("Mcp");

    [Fact]
    public void ServerName_DefaultsToGranit() =>
        new GranitMcpOptions().ServerName.ShouldBe("Granit");

    [Fact]
    public void ServerVersion_DefaultsToNull() =>
        new GranitMcpOptions().ServerVersion.ShouldBeNull();

    [Fact]
    public void ToolDiscovery_DefaultsToExplicit() =>
        new GranitMcpOptions().ToolDiscovery.ShouldBe(McpToolDiscoveryMode.Explicit);

    [Fact]
    public void EnableTenantFiltering_DefaultsToTrue() =>
        new GranitMcpOptions().EnableTenantFiltering.ShouldBeTrue();

    [Fact]
    public void MaxResponseSizeBytes_DefaultsTo51200() =>
        new GranitMcpOptions().MaxResponseSizeBytes.ShouldBe(51_200);
}
