using Granit.Mcp.Options;
using Granit.Mcp.Server.Internal;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Mcp.Server.Tests;

public sealed class ExplicitDiscoveryFilterTests
{
    [Fact]
    public async Task IsVisibleAsync_WhenAutoMode_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions
        {
            ToolDiscovery = McpToolDiscoveryMode.Auto,
        });
        ExplicitDiscoveryFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", typeof(NoAttributeClass), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenExplicitMode_WithMcpExposed_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions
        {
            ToolDiscovery = McpToolDiscoveryMode.Explicit,
        });
        ExplicitDiscoveryFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", typeof(ExposedClass), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenExplicitMode_WithoutMcpExposed_ShouldReturnFalse()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions
        {
            ToolDiscovery = McpToolDiscoveryMode.Explicit,
        });
        ExplicitDiscoveryFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", typeof(NoAttributeClass), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenExplicitMode_NullType_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions
        {
            ToolDiscovery = McpToolDiscoveryMode.Explicit,
        });
        ExplicitDiscoveryFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", toolType: null, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [McpExposed]
    private sealed class ExposedClass;

    private sealed class NoAttributeClass;
}
