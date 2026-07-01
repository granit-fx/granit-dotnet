using Granit.Modularity;
using Granit.Notifications.Endpoints;
using Granit.Validation;

namespace Granit.Notifications.WebPush.Endpoints;

/// <summary>
/// Granit module for the browser Web Push subscription HTTP endpoints.
/// </summary>
/// <remarks>
/// Opt-in companion to <see cref="GranitNotificationsEndpointsModule"/>: reference this package and
/// call <c>MapGranitWebPushSubscriptions()</c> to expose the subscription routes. Keeps the Web Push
/// channel (and its <c>Lib.Net.Http.WebPush</c> dependency) out of hosts that do not use Web Push.
/// </remarks>
[DependsOn(
    typeof(GranitNotificationsEndpointsModule),
    typeof(GranitNotificationsWebPushModule),
    typeof(GranitValidationModule))]
public sealed class GranitNotificationsWebPushEndpointsModule : GranitModule;
