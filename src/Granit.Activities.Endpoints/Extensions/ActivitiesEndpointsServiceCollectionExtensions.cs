using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Internal;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Activities.Endpoints.Extensions;

/// <summary>
/// Service-collection extensions for the <c>Granit.Activities.Endpoints</c>
/// module — wires the activity calendar cache invalidator (story #1801) onto
/// the framework's lifecycle event bus.
/// </summary>
public static class ActivitiesEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the four <see cref="ILocalEventHandler{TEvent}"/> wirings the
    /// activity calendar cache invalidator needs (Created, Updated, Deleted,
    /// BulkUpdated). Call from the host's composition root once after
    /// <c>AddGranitActivitiesEntityFrameworkCore()</c>.
    /// </summary>
    public static IServiceCollection AddGranitActivitiesCalendarCacheInvalidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ILocalEventHandler<EntityCreatedEvent<Activity>>, ActivityCalendarCacheInvalidator>();
        services.AddScoped<ILocalEventHandler<EntityUpdatedEvent<Activity>>, ActivityCalendarCacheInvalidator>();
        services.AddScoped<ILocalEventHandler<EntityDeletedEvent<Activity>>, ActivityCalendarCacheInvalidator>();
        services.AddScoped<ILocalEventHandler<EntityBulkUpdatedEvent<Activity>>, ActivityCalendarCacheInvalidator>();
        return services;
    }
}
