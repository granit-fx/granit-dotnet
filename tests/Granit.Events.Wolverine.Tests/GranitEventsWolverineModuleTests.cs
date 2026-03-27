using Granit.Events;
using Granit.Events.Wolverine.Internal;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class GranitEventsWolverineModuleTests
{
    [Fact]
    public void GranitEventsWolverineModule_IsGranitModule() =>
        typeof(GranitEventsWolverineModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitEventsWolverineModule_IsSealed() =>
        typeof(GranitEventsWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void DependsOn_DeclaresEventsAndWolverineModules()
    {
        DependsOnAttribute? attribute = typeof(GranitEventsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitEventsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_RegistersWolverineLocalEventBus()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        new GranitEventsWolverineModule().ConfigureServices(context);

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

        new GranitEventsWolverineModule().ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDistributedEventBus) &&
            d.ImplementationType == typeof(WolverineDistributedEventBus) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_RegistersWolverineDomainEventDispatcher()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        new GranitEventsWolverineModule().ConfigureServices(context);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDomainEventDispatcher) &&
            d.ImplementationType == typeof(WolverineDomainEventDispatcher) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
