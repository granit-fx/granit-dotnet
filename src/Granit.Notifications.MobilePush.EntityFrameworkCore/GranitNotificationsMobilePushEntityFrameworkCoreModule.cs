using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Notifications.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the mobile push channel.
/// </summary>
/// <remarks>
/// Opt-in companion to <see cref="GranitNotificationsEntityFrameworkCoreModule"/>: reference this
/// package and call <c>AddGranitNotificationsMobilePushEntityFrameworkCore(opts =&gt; opts.UseYourProvider(...))</c>
/// to replace the in-memory device token store with a durable EF Core implementation. Keeps the
/// mobile push token table out of hosts that do not use the channel.
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitNotificationsEntityFrameworkCoreModule),
    typeof(GranitNotificationsMobilePushModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitNotificationsMobilePushEntityFrameworkCoreModule : GranitModule
{
    // Services are registered via AddGranitNotificationsMobilePushEntityFrameworkCore() extension
    // method because it requires the DbContext configuration callback.
}
