using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Notifications.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Notifications.WebPush.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the W3C Web Push channel.
/// </summary>
/// <remarks>
/// Opt-in companion to <see cref="GranitNotificationsEntityFrameworkCoreModule"/>: reference this
/// package and call <c>AddGranitNotificationsWebPushEntityFrameworkCore(opts =&gt; opts.UseYourProvider(...))</c>
/// to replace the in-memory subscription store with a durable EF Core implementation. Keeps the
/// browser push subscription table out of hosts that do not use the channel.
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitNotificationsEntityFrameworkCoreModule),
    typeof(GranitNotificationsWebPushModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitNotificationsWebPushEntityFrameworkCoreModule : GranitModule
{
    // Services are registered via AddGranitNotificationsWebPushEntityFrameworkCore() extension
    // method because it requires the DbContext configuration callback.
}
