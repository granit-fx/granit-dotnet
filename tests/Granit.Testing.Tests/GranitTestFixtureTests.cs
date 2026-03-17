using Granit.Core.Modularity;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitTestFixtureTests : IAsyncDisposable
{
    private readonly GranitTestFixture<GranitTestingModule> _fixture = new();

    [Fact]
    public async Task BuildAsync_Bootstraps_Module_Graph()
    {
        await _fixture.BuildAsync();

        GranitApplication app = _fixture.GetRequiredService<GranitApplication>();
        app.ShouldNotBeNull();
    }

    [Fact]
    public async Task Fakes_Are_Injected_Into_Container()
    {
        await _fixture.BuildAsync();

        _fixture.GetRequiredService<ICurrentTenant>().ShouldBeSameAs(_fixture.Tenant);
        _fixture.GetRequiredService<ICurrentUserService>().ShouldBeSameAs(_fixture.User);
        _fixture.GetRequiredService<IClock>().ShouldBeSameAs(_fixture.Clock);
        _fixture.GetRequiredService<IGuidGenerator>().ShouldBeSameAs(_fixture.GuidGenerator);
    }

    [Fact]
    public async Task ConfigureServices_Callback_Can_Add_Services()
    {
        await _fixture.BuildAsync(services =>
            services.AddSingleton<ICustomTestService, CustomTestService>());

        _fixture.GetRequiredService<ICustomTestService>().ShouldNotBeNull();
    }

    [Fact]
    public void ServiceProvider_Throws_Before_Build()
    {
        Should.Throw<InvalidOperationException>(() => _ = _fixture.ServiceProvider);
    }

    [Fact]
    public async Task GetService_Returns_Null_For_Unregistered()
    {
        await _fixture.BuildAsync();

        _fixture.GetService<ICustomTestService>().ShouldBeNull();
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    private interface ICustomTestService;
    private sealed class CustomTestService : ICustomTestService;
}
