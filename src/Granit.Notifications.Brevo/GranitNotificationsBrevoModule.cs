using Granit.Core.Modularity;
using Granit.HttpResilience;
using Granit.Notifications.Email;
using Granit.Notifications.Sms;
using Granit.Notifications.WhatsApp;

namespace Granit.Notifications.Brevo;

/// <summary>
/// Granit module for the Brevo (ex-Sendinblue) multi-channel notification provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsBrevo()</c>.
/// Registers <c>BrevoNotificationProvider</c> as a keyed service for email, SMS and WhatsApp channels.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsEmailModule),
    typeof(GranitNotificationsSmsModule),
    typeof(GranitNotificationsWhatsAppModule))]
public sealed class GranitNotificationsBrevoModule : GranitModule;
