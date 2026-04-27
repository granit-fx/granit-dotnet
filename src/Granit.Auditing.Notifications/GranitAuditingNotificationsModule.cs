using Granit.Auditing.Notifications.Options;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.Notifications;

/// <summary>
/// Granit module for the auditing notification bridge.
/// Routes audit anomaly signals — repeated access-denied, role escalation,
/// impersonation, privileged configuration tampering — to platform administrators
/// and SOC operators via <c>Granit.Notifications</c>, with embedded HTML templates
/// shipped for the EN and FR baseline cultures.
/// </summary>
/// <remarks>
/// Operating model: the bridge subscribes to the firehose
/// <see cref="Auditing.Events.AuditEntryPersistedEto"/> and filters down to a
/// small, configurable set of <see cref="Auditing.Domain.AuditCategory"/> values
/// (see <see cref="AuditNotificationOptions"/>). This keeps the auditing core free
/// of any "is this entry interesting?" policy — the policy lives in configuration
/// where the SOC owns it.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitAuditingNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for the auditing notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitAuditingNotificationsModule).Assembly);

        // Layout glob — covers all snake_case notification names. The host application
        // registers the actual `Layout.Email` template; if absent, templates render
        // without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("auditing.*", "Layout.Email");

        // Bind the alertable-categories filter from configuration. ValidateOnStart
        // surfaces a misconfigured AlertableCategories list at boot rather than at
        // first event, which matters: a typo here means the SOC silently stops
        // receiving alerts.
        context.Services.AddOptions<AuditNotificationOptions>()
            .BindConfiguration(AuditNotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
