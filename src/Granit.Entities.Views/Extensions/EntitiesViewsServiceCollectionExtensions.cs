using Granit.Entities.Views.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Views.Extensions;

/// <summary>
/// DI extensions for the <c>Granit.Entities.Views</c> runtime module.
/// </summary>
public static class EntitiesViewsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default no-op <see cref="IEntityViewReader"/>
    /// (<see cref="NullEntityViewReader"/>). The EF companion replaces this
    /// binding via <c>services.Replace(...)</c>; hosts that stop at this layer
    /// still boot — every view lookup returns empty so the renderer falls
    /// through to the compiled default collection.
    /// </summary>
    public static IServiceCollection AddGranitEntitiesViews(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IEntityViewReader, NullEntityViewReader>();
        return services;
    }
}
