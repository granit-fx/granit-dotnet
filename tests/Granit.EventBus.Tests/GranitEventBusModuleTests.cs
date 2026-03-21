using Granit.Core.Events;
using Granit.Core.Modularity;
using Granit.EventBus.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Tests;

public sealed class GranitEventBusModuleTests : IDisposable
{
    private readonly ServiceProvider _sp;

    public GranitEventBusModuleTests()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();

        GranitEventBusModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        _sp = builder.Services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void ConfigureServices_RegistersLocalEventBus()
    {
        using IServiceScope scope = _sp.CreateScope();
        ILocalEventBus bus = scope.ServiceProvider.GetRequiredService<ILocalEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersDistributedEventBus()
    {
        using IServiceScope scope = _sp.CreateScope();
        IDistributedEventBus bus = scope.ServiceProvider.GetRequiredService<IDistributedEventBus>();

        bus.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersEventBusMetrics()
    {
        EventBusMetrics metrics = _sp.GetRequiredService<EventBusMetrics>();

        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitEventBusModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
