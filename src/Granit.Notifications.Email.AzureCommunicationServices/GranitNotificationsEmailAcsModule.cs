using Granit.Modularity;
using Granit.Notifications.Email.AzureCommunicationServices.Extensions;

namespace Granit.Notifications.Email.AzureCommunicationServices;

/// <summary>Module for Azure Communication Services email provider.</summary>
[DependsOn(typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsEmailAcsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsEmailAcs();
}
