using Granit.Http.Resilience;
using Granit.Modularity;
using Granit.Notifications.GoogleFcm.Extensions;
using Granit.Notifications.MobilePush;
using Granit.Timing;

namespace Granit.Notifications.GoogleFcm;

/// <summary>
/// Granit module for the Firebase Cloud Messaging (FCM) mobile push provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsGoogleFcm()</c>.
/// Registers <c>GoogleFcmMobilePushSender</c> as a keyed <c>IMobilePushSender</c> implementation.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsMobilePushModule),
    typeof(GranitTimingModule))]
public sealed class GranitNotificationsGoogleFcmModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsGoogleFcm();
}
