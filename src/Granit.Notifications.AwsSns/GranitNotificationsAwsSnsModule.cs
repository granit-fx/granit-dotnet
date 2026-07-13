using Granit.Modularity;
using Granit.Notifications.AwsSns.MobilePush.Extensions;
using Granit.Notifications.AwsSns.MobilePush.Options;
using Granit.Notifications.AwsSns.Sms.Extensions;
using Granit.Notifications.AwsSns.Sms.Options;
using Granit.Notifications.MobilePush;
using Granit.Notifications.Sms;
using Microsoft.Extensions.Configuration;

namespace Granit.Notifications.AwsSns;

/// <summary>
/// Granit module for the AWS SNS notification provider — a multi-capability provider
/// implementing both <c>ISmsSender</c> (SNS direct publish) and <c>IMobilePushSender</c>
/// (SNS platform endpoints), keyed "AwsSns".
/// </summary>
/// <remarks>
/// Each capability is wired only when its configuration section exists
/// (<c>Notifications:AwsSns:Sms</c> / <c>Notifications:AwsSns:MobilePush</c>): an
/// SMS-only host never registers — nor startup-validates — the mobile push sender.
/// The per-capability <c>AddGranitNotificationsAwsSns*</c> extensions remain available
/// for explicit opt-in without configuration.
/// </remarks>
[DependsOn(
    typeof(GranitNotificationsMobilePushModule),
    typeof(GranitNotificationsSmsModule))]
public sealed class GranitNotificationsAwsSnsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        if (context.Configuration.GetSection(AwsSnsSmsOptions.SectionName).Exists())
        {
            context.Services.AddGranitNotificationsAwsSnsSms();
        }

        if (context.Configuration.GetSection(AwsSnsMobilePushOptions.SectionName).Exists())
        {
            context.Services.AddGranitNotificationsAwsSnsMobilePush();
        }
    }
}
