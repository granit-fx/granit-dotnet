using Granit.AI.Mcp.Internal;
using Granit.AI.Mcp.Options;
using Granit.Mcp.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Mcp.Tests;

public sealed class McpToolSourceProviderTests
{
    [Fact]
    public async Task GetToolsAsync_WhenNoConnections_ShouldReturnEmpty()
    {
        IMcpClientFactory factory = Substitute.For<IMcpClientFactory>();
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions
        {
            ToolSourceConnections = [],
        });

        McpToolSourceProvider sut = new(factory, options);

        IReadOnlyList<AITool> tools = await sut.GetToolsAsync("default", TestContext.Current.CancellationToken);

        tools.ShouldBeEmpty();
        await factory.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
