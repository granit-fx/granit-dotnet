using Granit.Core.Modularity;
using Granit.HttpResilience;
using Granit.Notifications.Email.Scaleway.Extensions;

namespace Granit.Notifications.Email.Scaleway;

/// <summary>Module for Scaleway Transactional Email provider.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsEmailScalewayModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsEmailScaleway();
}
