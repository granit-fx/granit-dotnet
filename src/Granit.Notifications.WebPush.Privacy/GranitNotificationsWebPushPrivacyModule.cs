using Granit.Modularity;
using Granit.Notifications.WebPush.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Notifications.WebPush.Privacy;

/// <summary>
/// Granit module wiring browser Web Push subscriptions into the privacy framework: export provider (Art. 15)
/// and Wolverine deletion handler (Art. 17), so push identifiers no longer survive a
/// GDPR erasure nor go missing from an export.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsWebPushModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitNotificationsWebPushPrivacyModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<WebPushPrivacyDataProvider>();
}
