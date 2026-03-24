using Granit.Modularity;
using Granit.Notifications.Sms.AwsSns.Extensions;

namespace Granit.Notifications.Sms.AwsSns;

/// <summary>Module for AWS SNS SMS provider.</summary>
[DependsOn(typeof(GranitNotificationsSmsModule))]
public sealed class GranitNotificationsSmsAwsSnsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsSmsAwsSns();
}
