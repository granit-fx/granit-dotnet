using Granit.Http.Resilience;
using Granit.Modularity;

namespace Granit.Notifications.MobilePush.GoogleFcm;

/// <summary>
/// Granit module for the Firebase Cloud Messaging (FCM) mobile push provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsMobilePushGoogleFcm()</c>.
/// Registers <c>GoogleFcmMobilePushSender</c> as a keyed <c>IMobilePushSender</c> implementation.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsMobilePushModule))]
public sealed class GranitNotificationsMobilePushGoogleFcmModule : GranitModule;
