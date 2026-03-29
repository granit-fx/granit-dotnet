using Granit.Http.Resilience;
using Granit.Modularity;

namespace Granit.Notifications.Zulip;

/// <summary>
/// Granit module for the Zulip chat notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsZulip()</c>.
/// Includes its own <c>ZulipBotSender</c> implementation.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsZulipModule : GranitModule;
