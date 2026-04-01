using Granit.Modularity;
using Granit.Templating;

namespace Granit.Notifications.Email;

/// <summary>
/// Granit module for the email notification channel.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsEmail()</c>.
/// Providers (SMTP, Brevo) register keyed <c>IEmailSender</c> implementations.
/// Embeds localized <c>Notifications.Default</c> fallback templates for when
/// no type-specific template is registered.
/// </remarks>
[DependsOn(typeof(GranitNotificationsAbstractionsModule))]
[DependsOn(typeof(GranitTemplatingModule))]
public sealed class GranitNotificationsEmailModule : GranitModule;
