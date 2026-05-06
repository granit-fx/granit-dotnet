using Granit.Activities.Diagnostics;
using Granit.Activities.Domain;
using Granit.Activities.Exports;
using Granit.Activities.Extensions;
using Granit.Activities.Internal;
using Granit.Activities.Metrics;
using Granit.Activities.Queries;
using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Activities.Extensions;

/// <summary>
/// DI extensions for the <c>Granit.Activities</c> runtime module.
/// </summary>
public static class ActivitiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the runtime <see cref="IActivityRegistry"/> plus the framework's
    /// <see cref="StandardActivityTypeProvider"/> (ToDo / Call / Meeting / Email),
    /// the <see cref="ActivityQueryDefinition"/> / <see cref="ActivityExportDefinition"/>
    /// pairing for the admin grid, the open / overdue activity-count metrics, and
    /// the <see cref="ActivitiesMetrics"/> meter + <c>Granit.Activities</c>
    /// <see cref="System.Diagnostics.ActivitySource"/>.
    /// Hosts that want a strictly custom catalog can call this and then remove
    /// the standard provider before <c>BuildServiceProvider</c>.
    /// </summary>
    public static IServiceCollection AddGranitActivities(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(ActivitiesActivitySource.Name);
        services.TryAddSingleton<ActivitiesMetrics>();

        services.AddActivityTypeProvider<StandardActivityTypeProvider>();
        services.AddSingleton<IActivityRegistry, ActivityRegistry>();
        services.AddQueryDefinition<Activity, ActivityQueryDefinition>();
        services.AddExportDefinition<Activity, ActivityExportDefinition>();
        services.AddMetricDefinition<Activity, int, OpenActivityCountMetricDefinition>();
        services.AddMetricDefinition<Activity, int, OverdueActivityCountMetricDefinition>();
        return services;
    }
}
