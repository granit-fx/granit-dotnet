using Microsoft.Extensions.DependencyInjection;

namespace Granit.Entities.Actions;

/// <summary>
/// Service-collection extensions for registering cross-module entity action
/// contributors.
/// </summary>
public static class EntityActionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContributor"/> as a singleton implementing
    /// <see cref="IEntityActionContributor"/>. Multiple contributors per host
    /// are expected — invocation order at boot is registration order.
    /// </summary>
    public static IServiceCollection AddEntityActionContribution<TContributor>(this IServiceCollection services)
        where TContributor : class, IEntityActionContributor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IEntityActionContributor, TContributor>();
        return services;
    }
}
