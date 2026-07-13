using Granit.Modularity;
using Granit.Notifications.AwsSes.Extensions;
using Granit.Notifications.Email;

namespace Granit.Notifications.AwsSes;

/// <summary>Module for Amazon SES email provider.</summary>
[DependsOn(typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsAwsSesModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsAwsSes();
}
