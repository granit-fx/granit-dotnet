using Granit.Mcp.Client.Internal;
using Granit.Mcp.Client.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Mcp.Client.Tests;

public sealed class DefaultMcpClientFactoryTests
{
    [Fact]
    public async Task CreateAsync_WhenConnectionNotConfigured_ShouldThrow()
    {
        IOptions<GranitMcpClientOptions> options = MsOptions.Create(new GranitMcpClientOptions());
        DefaultMcpClientFactory sut = new(options);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync("nonexistent", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("nonexistent");
        ex.Message.ShouldContain("not configured");
    }

    [Fact]
    public async Task CreateAsync_WhenUnsupportedTransport_ShouldThrow()
    {
        IOptions<GranitMcpClientOptions> options = MsOptions.Create(new GranitMcpClientOptions
        {
            Connections = new Dictionary<string, McpConnectionOptions>
            {
                ["bad"] = new() { Transport = "websocket", Url = "ws://localhost" },
            },
        });
        DefaultMcpClientFactory sut = new(options);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync("bad", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("websocket");
        ex.Message.ShouldContain("Unsupported");
    }

    [Fact]
    public async Task CreateAsync_WhenHttpWithoutUrl_ShouldThrow()
    {
        IOptions<GranitMcpClientOptions> options = MsOptions.Create(new GranitMcpClientOptions
        {
            Connections = new Dictionary<string, McpConnectionOptions>
            {
                ["nourl"] = new() { Transport = "http" },
            },
        });
        DefaultMcpClientFactory sut = new(options);

        await Should.ThrowAsync<ArgumentException>(
            () => sut.CreateAsync("nourl", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_WhenStdioWithoutCommand_ShouldThrow()
    {
        IOptions<GranitMcpClientOptions> options = MsOptions.Create(new GranitMcpClientOptions
        {
            Connections = new Dictionary<string, McpConnectionOptions>
            {
                ["nocmd"] = new() { Transport = "stdio" },
            },
        });
        DefaultMcpClientFactory sut = new(options);

        await Should.ThrowAsync<ArgumentException>(
            () => sut.CreateAsync("nocmd", TestContext.Current.CancellationToken));
    }
}
