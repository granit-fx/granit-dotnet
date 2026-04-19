using Granit.Modularity;
using Granit.Notifications.Privacy.Wolverine.DataExport;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Notifications.Privacy.Wolverine;

/// <summary>
/// Granit module that registers the <see cref="NotificationsPrivacyDataProvider"/> so the
/// notifications module participates in the privacy export scatter-gather saga.
/// </summary>
/// <remarks>
/// Kept separate from <c>Granit.Notifications.Wolverine</c> so apps that only use the
/// durable dispatch integration do not inherit Privacy + BlobStorage dependencies.
/// The matching Wolverine handler is discovered automatically. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitNotificationsPrivacyProvider())</c>.
/// </remarks>
[DependsOn(
    typeof(GranitNotificationsModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitNotificationsPrivacyWolverineModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<NotificationsPrivacyDataProvider>();
}
