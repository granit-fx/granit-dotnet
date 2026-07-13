using Granit.Modularity;
using Granit.Notifications.AzureCommunicationServices.Email.Extensions;
using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Granit.Notifications.AzureCommunicationServices.Sms.Extensions;
using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Granit.Notifications.Email;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Configuration;

namespace Granit.Notifications.AzureCommunicationServices;

/// <summary>
/// Granit module for the Azure Communication Services notification provider — a
/// multi-capability provider implementing both <c>IEmailSender</c> and <c>ISmsSender</c>,
/// keyed "AzureCommunicationServices".
/// </summary>
/// <remarks>
/// Each capability is wired only when its configuration section exists
/// (<c>Notifications:AzureCommunicationServices:Email</c> /
/// <c>Notifications:AzureCommunicationServices:Sms</c>): an email-only host never
/// registers — nor startup-validates — the SMS sender. The per-capability
/// <c>AddGranitNotificationsAcs*</c> extensions remain available for explicit opt-in
/// without configuration.
/// </remarks>
[DependsOn(
    typeof(GranitNotificationsEmailModule),
    typeof(GranitNotificationsSmsModule))]
public sealed class GranitNotificationsAzureCommunicationServicesModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        if (context.Configuration.GetSection(AcsEmailOptions.SectionName).Exists())
        {
            context.Services.AddGranitNotificationsAcsEmail();
        }

        if (context.Configuration.GetSection(AcsSmsOptions.SectionName).Exists())
        {
            context.Services.AddGranitNotificationsAcsSms();
        }
    }
}
