using Granit.Modularity;
using Granit.Notifications.Email;

namespace Granit.Notifications.Email.Smtp;

/// <summary>
/// Granit module for the SMTP email provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitNotificationsEmailSmtp()</c>.
/// Registers <c>SmtpEmailSender</c> as a keyed <c>IEmailSender</c> implementation.
/// </remarks>
[DependsOn(typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsEmailSmtpModule : GranitModule;
