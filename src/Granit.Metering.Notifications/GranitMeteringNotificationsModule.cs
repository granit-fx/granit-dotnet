using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Metering.Notifications;

/// <summary>
/// Granit module for the metering notification bridge.
/// Routes <c>QuotaThresholdReachedEto</c> and <c>QuotaExceededEto</c> integration events
/// to tenant administrators via <c>Granit.Notifications</c>, so they can upgrade or
/// rebalance their plan before throttling kicks in.
/// </summary>
[DependsOn(
    typeof(GranitMeteringModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitMeteringNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for both metering quota notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitMeteringNotificationsModule).Assembly);

        // Layout glob — covers metering.quota_warning and metering.quota_exceeded.
        // The host application registers the actual `Layout.Email` template; if absent,
        // templates render without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("metering.*", "Layout.Email");
    }
}
