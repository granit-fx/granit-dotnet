using Granit.Events.Diagnostics;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Events.Tests;

public sealed class GranitEventsModuleTests : IDisposable
{
    private readonly ServiceProvider _sp;

    public GranitEventsModuleTests()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddMetrics();
        builder.Services.AddLogging();

        GranitEventsModule module = new();
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
    public void ConfigureServices_RegistersEventsMetrics()
    {
        EventsMetrics metrics = _sp.GetRequiredService<EventsMetrics>();

        metrics.ShouldNotBeNull();
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        GranitEventsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
