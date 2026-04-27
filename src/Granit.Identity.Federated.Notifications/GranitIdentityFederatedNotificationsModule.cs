using Granit.Identity.Federated;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Federated.Notifications;

/// <summary>
/// Granit module for the federated identity notification bridge.
/// Routes federated identity audit events — token exchange / impersonation,
/// GDPR Art. 17 user-deletion receipts — to platform / tenant administrators
/// via <c>Granit.Notifications</c>, with embedded HTML templates shipped for
/// the EN and FR baseline cultures.
/// </summary>
/// <remarks>
/// Operating model: the bridge subscribes to integration events emitted by
/// <see cref="GranitIdentityFederatedModule"/> (token-exchange audit trail,
/// IdP-driven user deletions). Recipients are resolved via the standard
/// notifications subscription mechanism so the bridge owns no platform-admin
/// or tenant-admin roster lookup logic — operators opt in through the
/// notifications admin UI. Coverage is scoped by ISO 27001 A.12.4 (logging
/// and monitoring of privileged operations) and GDPR Art. 17 (right to
/// erasure receipts).
/// </remarks>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitIdentityFederatedNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for the federated identity notifications.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitIdentityFederatedNotificationsModule).Assembly);

        // Layout glob — covers all snake_case notification names. The host application
        // registers the actual `Layout.Email` template; if absent, templates render
        // without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("identity.*", "Layout.Email");
    }
}
