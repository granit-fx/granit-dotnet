using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Notifications.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence in the notification engine.
/// </summary>
/// <remarks>
/// Replaces the default InMemory/no-op stores with durable EF Core implementations.
/// The application must configure the DbContext via
/// <c>AddGranitNotificationsEntityFrameworkCore(opts => opts.UseYourProvider(connectionString))</c>
/// instead of using this module directly when custom DbContext options are needed.
/// </remarks>
[DependsOn(typeof(GranitNotificationsModule))]
[DependsOn(typeof(GranitPersistenceModule))]
public sealed class GranitNotificationsEntityFrameworkCoreModule : GranitModule
{
    // Services are registered via AddGranitNotificationsEntityFrameworkCore() extension method
    // because it requires the DbContext configuration callback.
}
