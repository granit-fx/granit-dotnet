using Granit.Http.Resilience;
using Granit.Modularity;
using Granit.Notifications.Email;
using Granit.Notifications.SendGrid.Extensions;

namespace Granit.Notifications.SendGrid;

/// <summary>Module for SendGrid email provider.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsSendGridModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsSendGrid();
}
