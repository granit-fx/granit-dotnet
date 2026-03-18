using Granit.Core.Events;
using Granit.Core.Modularity;
using Granit.EventBus;
using Granit.EventBus.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.EventBus.Wolverine.Tests;

public sealed class GranitEventBusWolverineModuleTests
{
    [Fact]
    public void GranitEventBusWolverineModule_IsGranitModule() =>
        typeof(GranitEventBusWolverineModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitEventBusWolverineModule_IsSealed() =>
        typeof(GranitEventBusWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void DependsOn_DeclaresEventBusAndWolverineModules()
    {
        DependsOnAttribute? attribute = typeof(GranitEventBusWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitEventBusModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_RegistersWolverineLocalEventBus()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        new GranitEventBusWolverineModule().ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ILocalEventBus) &&
            d.ImplementationType == typeof(WolverineLocalEventBus) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_RegistersWolverineDistributedEventBus()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        new GranitEventBusWolverineModule().ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDistributedEventBus) &&
            d.ImplementationType == typeof(WolverineDistributedEventBus) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
