using Granit.Http.Resilience;
using Granit.Modularity;
using Granit.Notifications.Email;
using Granit.Notifications.Scaleway.Extensions;

namespace Granit.Notifications.Scaleway;

/// <summary>Module for Scaleway Transactional Email provider.</summary>
[DependsOn(
    typeof(GranitHttpResilienceModule),
    typeof(GranitNotificationsEmailModule))]
public sealed class GranitNotificationsScalewayModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitNotificationsScaleway();
}
