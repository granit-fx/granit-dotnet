using Granit.Modularity;
using Granit.Notifications.MobilePush.AwsSns.Extensions;

namespace Granit.Notifications.MobilePush.AwsSns;

/// <summary>Module for AWS SNS mobile push provider.</summary>
[DependsOn(typeof(GranitNotificationsMobilePushModule))]
public sealed class GranitNotificationsMobilePushAwsSnsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsMobilePushAwsSns();
}
