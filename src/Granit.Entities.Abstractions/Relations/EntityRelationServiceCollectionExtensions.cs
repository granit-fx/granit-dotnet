using Microsoft.Extensions.DependencyInjection;

namespace Granit.Entities.Relations;

/// <summary>
/// Service-collection extensions for registering cross-module entity relation
/// contributors.
/// </summary>
public static class EntityRelationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContributor"/> as a singleton implementing
    /// <see cref="IEntityRelationContributor"/>. Multiple contributors per host
    /// are expected — invocation order at boot is registration order.
    /// </summary>
    public static IServiceCollection AddEntityRelationContribution<TContributor>(this IServiceCollection services)
        where TContributor : class, IEntityRelationContributor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IEntityRelationContributor, TContributor>();
        return services;
    }
}
