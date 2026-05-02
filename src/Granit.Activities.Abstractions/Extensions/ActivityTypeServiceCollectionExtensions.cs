using Microsoft.Extensions.DependencyInjection;

namespace Granit.Activities.Extensions;

/// <summary>
/// Service-collection extensions for registering activity-type providers.
/// </summary>
public static class ActivityTypeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as a singleton implementing
    /// <see cref="IActivityTypeProvider"/>. Multiple providers per host are
    /// expected — the runtime registry (story A2) aggregates every registered
    /// provider's contributions into the canonical
    /// <see cref="IActivityRegistry"/>.
    /// </summary>
    public static IServiceCollection AddActivityTypeProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IActivityTypeProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IActivityTypeProvider, TProvider>();
        return services;
    }
}
