using Granit.Diagnostics;
using Granit.Http.Resilience;
using Granit.Modularity;
using Granit.Notifications.Zulip.Extensions;

namespace Granit.Notifications.Zulip;

/// <summary>
/// Granit module for the Zulip chat notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsZulip()</c>.
/// Includes its own <c>ZulipBotSender</c> implementation.
/// </remarks>
[DependsOn(
    typeof(GranitDiagnosticsModule),
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsZulipModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsZulip();
}
