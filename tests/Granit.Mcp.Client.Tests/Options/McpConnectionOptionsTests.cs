using Granit.Mcp.Client.Options;
using Shouldly;

namespace Granit.Mcp.Client.Tests.Options;

public sealed class McpConnectionOptionsTests
{
    [Fact]
    public void Url_DefaultsToNull() =>
        new McpConnectionOptions().Url.ShouldBeNull();

    [Fact]
    public void Transport_DefaultsToHttp() =>
        new McpConnectionOptions().Transport.ShouldBe("http");

    [Fact]
    public void AllowInsecureTransport_DefaultsToFalse() =>
        new McpConnectionOptions().AllowInsecureTransport.ShouldBeFalse();

    [Fact]
    public void Command_DefaultsToNull() =>
        new McpConnectionOptions().Command.ShouldBeNull();

    [Fact]
    public void Arguments_DefaultsToEmpty() =>
        new McpConnectionOptions().Arguments.ShouldBeEmpty();
}
