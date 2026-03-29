using Granit.Modularity;

namespace Granit.Notifications.Email;

/// <summary>
/// Granit module for the email notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsEmail()</c>.
/// Providers (SMTP, Brevo) register keyed <c>IEmailSender</c> implementations.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitNotificationsEmailModule : GranitModule;
