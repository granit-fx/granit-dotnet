using Granit.Mcp.Client.Options;
using Shouldly;

namespace Granit.Mcp.Client.Tests.Options;

public sealed class GranitMcpClientOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        GranitMcpClientOptions.SectionName.ShouldBe("Mcp:Client");

    [Fact]
    public void Connections_DefaultsToEmptyDictionary()
    {
        var options = new GranitMcpClientOptions();

        options.Connections.ShouldNotBeNull();
        options.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void Connections_CanBePopulated()
    {
        var options = new GranitMcpClientOptions();
        options.Connections["server1"] = new McpConnectionOptions { Url = "http://localhost:5000" };

        options.Connections.ShouldContainKey("server1");
        options.Connections["server1"].Url.ShouldBe("http://localhost:5000");
    }
}
