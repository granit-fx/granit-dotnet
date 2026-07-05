using Granit.Mcp.Options;
using Granit.Mcp.Server.Internal;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Mcp.Server.Tests;

public sealed class TenantAwareVisibilityFilterTests
{
    [Fact]
    public async Task IsVisibleAsync_WhenFilteringDisabled_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions
        {
            EnableTenantFiltering = false,
        });
        TenantAwareVisibilityFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", typeof(TenantScopedClass), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenNoTenantScopeAttribute_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions());
        TenantAwareVisibilityFilter sut = new(options);

        bool result = await sut.IsVisibleAsync("tool", typeof(NonScopedClass), Substitute.For<IServiceProvider>(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenTenantRequired_AndTenantAvailable_ShouldReturnTrue()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions());
        TenantAwareVisibilityFilter sut = new(options);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);

        ServiceCollection services = new();
        services.AddSingleton(tenant);
        await using ServiceProvider sp = services.BuildServiceProvider();

        bool result = await sut.IsVisibleAsync("tool", typeof(TenantScopedClass), sp, TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsVisibleAsync_WhenTenantRequired_AndNoTenant_ShouldReturnFalse()
    {
        IOptions<GranitMcpOptions> options = MsOptions.Create(new GranitMcpOptions());
        TenantAwareVisibilityFilter sut = new(options);

        // No ICurrentTenant registered
        ServiceCollection services = new();
        await using ServiceProvider sp = services.BuildServiceProvider();

        bool result = await sut.IsVisibleAsync("tool", typeof(TenantScopedClass), sp, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [McpTenantScope(RequireTenant = true)]
    private sealed class TenantScopedClass;

    private sealed class NonScopedClass;
}
