using Granit.Activities.Extensions;
using Granit.Activities.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Activities.Extensions;

/// <summary>
/// DI extensions for the <c>Granit.Activities</c> runtime module.
/// </summary>
public static class ActivitiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the runtime <see cref="IActivityRegistry"/> plus the framework's
    /// <see cref="StandardActivityTypeProvider"/> (ToDo / Call / Meeting / Email).
    /// Hosts that want a strictly custom catalog can call this and then remove
    /// the standard provider before <c>BuildServiceProvider</c>.
    /// </summary>
    public static IServiceCollection AddGranitActivities(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddActivityTypeProvider<StandardActivityTypeProvider>();
        services.AddSingleton<IActivityRegistry, ActivityRegistry>();
        return services;
    }
}
