using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Mcp.Server.Tests;

public sealed class ModuleScopeVisibilityFilterTests
{
    [Fact]
    public async Task IsVisibleAsync_WhenEnabledModulesEmpty_ShouldReturnTrue()
    {
        IOptions<GranitMcpServerOptions> options = MsOptions.Create(new GranitMcpServerOptions());
        ModuleScopeVisibilityFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("list_blobs", typeof(FakeBlobTool), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenModuleEnabled_ShouldReturnTrue()
    {
        // FakeBlobTool is in Granit.Mcp.Server.Tests namespace
        IOptions<GranitMcpServerOptions> options = MsOptions.Create(new GranitMcpServerOptions
        {
            EnabledModules = ["Server"],
        });
        ModuleScopeVisibilityFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("list_blobs", typeof(FakeBlobTool), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenModuleNotEnabled_ShouldReturnFalse()
    {
        IOptions<GranitMcpServerOptions> options = MsOptions.Create(new GranitMcpServerOptions
        {
            EnabledModules = ["Workflow"],
        });
        ModuleScopeVisibilityFilter sut = new(options);

        // FakeBlobTool is in Granit.Mcp.Server.Tests, not Granit.Workflow
        bool result = await sut.IsVisibleAsync("list_blobs", typeof(FakeBlobTool), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenToolTypeIsNull_ShouldReturnFalse()
    {
        IOptions<GranitMcpServerOptions> options = MsOptions.Create(new GranitMcpServerOptions
        {
            EnabledModules = ["BlobStorage"],
        });
        ModuleScopeVisibilityFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("unknown", toolType: null, Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    /// <summary>Fake tool type for module scope testing.</summary>
    internal sealed class FakeBlobTool;
}
