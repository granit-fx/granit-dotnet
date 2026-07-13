using Granit.Modularity;
using Granit.Notifications.Email;
using Granit.Notifications.Smtp.Extensions;

namespace Granit.Notifications.Smtp;

/// <summary>
/// Granit module for the SMTP email provider.
/// </summary>
/// <remarks>
/// Self-registers the SMTP sender (EmailChannelOptions.Provider defaults to "Smtp", so the
/// out-of-box path must work when this module is referenced).
/// Registers <c>SmtpEmailSender</c> as a keyed <c>IEmailSender</c> implementation.
/// </remarks>
[DependsOn(typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsSmtpModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsSmtp();
}
