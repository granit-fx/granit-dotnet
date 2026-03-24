using Granit.Modularity;
using Granit.Notifications.Sms.AzureCommunicationServices.Extensions;

namespace Granit.Notifications.Sms.AzureCommunicationServices;

/// <summary>Module for Azure Communication Services SMS provider.</summary>
[DependsOn(typeof(GranitNotificationsSmsModule))]
public sealed class GranitNotificationsSmsAcsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsSmsAcs();
}
