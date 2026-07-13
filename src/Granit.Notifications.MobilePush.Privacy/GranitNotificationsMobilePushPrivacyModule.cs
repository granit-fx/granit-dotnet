using Granit.Modularity;
using Granit.Notifications.MobilePush.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Notifications.MobilePush.Privacy;

/// <summary>
/// Granit module wiring mobile push device tokens into the privacy framework: export provider (Art. 15)
/// and Wolverine deletion handler (Art. 17), so push identifiers no longer survive a
/// GDPR erasure nor go missing from an export.
/// </summary>
[DependsOn(
    typeof(GranitNotificationsMobilePushModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitNotificationsMobilePushPrivacyModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<MobilePushPrivacyDataProvider>();
}
