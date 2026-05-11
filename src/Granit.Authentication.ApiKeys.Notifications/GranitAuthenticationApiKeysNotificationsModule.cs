using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.ApiKeys.Notifications;

/// <summary>
/// Granit module for the API-keys notification bridge.
/// Routes API key lifecycle events (creation, rotation, revocation) to tenant administrators
/// via <c>Granit.Notifications</c>, supporting least-privilege rotation policy
/// (ISO 27001 A.9.4) and avoiding service disruption from silent key expirations.
/// </summary>
/// <remarks>
/// <para>
/// Templates redact secret material — only the public-safe key metadata (id, name, prefix)
/// reaches the notification payload. The raw key value is never persisted by Granit
/// (only its SHA-256 hash is) and the bridge takes care never to forward that hash to
/// recipient-bound rendering.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuthenticationApiKeysModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitAuthenticationApiKeysNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for every API-keys notification.
        // Apps can override any of them at runtime through the Granit.Templating
        // admin API (DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitAuthenticationApiKeysNotificationsModule).Assembly);

        // Layout glob — covers all snake_case "apikeys.*" notification names. The host
        // application registers the actual `Layout.Email` template; if absent, templates
        // render without layout (warning logged, no crash).
        context.Services.AddTemplateLayout("apikeys.*", "Layout.Email");
    }
}
