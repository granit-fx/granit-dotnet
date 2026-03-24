using Granit.Diagnostics;
using Granit.Events;
using Granit.Events.Diagnostics;
using Granit.Events.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Events.Extensions;

/// <summary>
/// Extensions for registering in-process event bus providers.
/// </summary>
public static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default in-process event bus providers.
    /// </summary>
    /// <remarks>
    /// Uses <c>TryAdd</c> so that provider packages (e.g., <c>Granit.Events.Wolverine</c>)
    /// can replace these defaults by registering before or after this call.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEvents(this IServiceCollection services)
    {
        services.TryAddSingleton<EventsMetrics>();
        services.TryAddScoped<ILocalEventBus, InProcessLocalEventBus>();
        services.TryAddScoped<IDistributedEventBus, InProcessDistributedEventBus>();
        GranitActivitySourceRegistry.Register(EventsActivitySource.Name);
        return services;
    }
}
