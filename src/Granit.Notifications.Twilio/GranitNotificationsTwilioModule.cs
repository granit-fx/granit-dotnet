using Granit.Core.Modularity;
using Granit.HttpResilience;
using Granit.Notifications.Sms;
using Granit.Notifications.WhatsApp;

namespace Granit.Notifications.Twilio;

/// <summary>
/// Granit module for the Twilio multi-channel notification provider (SMS + WhatsApp).
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsTwilio()</c>.
/// Registers <c>TwilioNotificationProvider</c> as a keyed service for SMS and WhatsApp channels.
/// </remarks>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsSmsModule),
    typeof(GranitNotificationsWhatsAppModule))]
public sealed class GranitNotificationsTwilioModule : GranitModule;
