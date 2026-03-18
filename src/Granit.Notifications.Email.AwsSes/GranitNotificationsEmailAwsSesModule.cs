using Granit.Core.Modularity;
using Granit.Notifications.Email.AwsSes.Extensions;

namespace Granit.Notifications.Email.AwsSes;

/// <summary>Module for Amazon SES email provider.</summary>
public sealed class GranitNotificationsEmailAwsSesModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsEmailAwsSes();
}
