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

    [Fact]
    public void AllProperties_CanBeSet()
    {
        McpConnectionOptions options = new()
        {
            Url = "https://mcp.example.com",
            Transport = "stdio",
            AllowInsecureTransport = true,
            Command = "dotnet",
            Arguments = ["run", "--project", "MyServer"],
        };

        options.Url.ShouldBe("https://mcp.example.com");
        options.Transport.ShouldBe("stdio");
        options.AllowInsecureTransport.ShouldBeTrue();
        options.Command.ShouldBe("dotnet");
        options.Arguments.ShouldBe(["run", "--project", "MyServer"]);
    }
}
