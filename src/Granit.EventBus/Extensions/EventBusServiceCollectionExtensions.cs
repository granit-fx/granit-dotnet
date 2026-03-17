using Granit.Core.Events;
using Granit.EventBus.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.EventBus.Extensions;

/// <summary>
/// Extensions for registering in-process event bus providers.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default in-process event bus providers.
    /// </summary>
    /// <remarks>
    /// Uses <c>TryAdd</c> so that provider packages (e.g., <c>Granit.EventBus.Wolverine</c>)
    /// can replace these defaults by registering before or after this call.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEventBus(this IServiceCollection services)
    {
        services.TryAddScoped<ILocalEventBus, InProcessLocalEventBus>();
        services.TryAddScoped<IDistributedEventBus, InProcessDistributedEventBus>();
        return services;
    }
}
