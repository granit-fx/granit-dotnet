using Granit.Modularity;

namespace Granit.Notifications.WhatsApp;

/// <summary>
/// Granit module for the WhatsApp notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsWhatsApp()</c>.
/// Providers register keyed <c>IWhatsAppSender</c> implementations.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsWhatsAppModule : GranitModule;
