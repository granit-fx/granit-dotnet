using Granit.AI.Endpoints.Extensions;
using Granit.Auditing.Endpoints.Extensions;
using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Granit.Authorization.Endpoints.Extensions;
using Granit.BackgroundJobs.Endpoints.Extensions;
using Granit.Bff.Endpoints.Extensions;
using Granit.BlobStorage.Endpoints.Extensions;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataLookup.Endpoints.Extensions;
using Granit.Diagnostics.Endpoints.Extensions;
using Granit.Features.Endpoints.Extensions;
using Granit.Hostnames.Endpoints.Extensions;
using Granit.Http.Cookies.Endpoints.Extensions;
using Granit.Http.SecurityHeaders.Endpoints.Extensions;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Localization.Endpoints.Extensions;
using Granit.MultiTenancy.Endpoints.Extensions;
using Granit.Notifications.Endpoints.Extensions;
using Granit.OpenIddict.Endpoints.Extensions;
using Granit.Presence.Endpoints.Extensions;
using Granit.Privacy.Endpoints.Discovery;
using Granit.Privacy.Endpoints.Extensions;
using Granit.Scheduling.Endpoints.Extensions;
using Granit.Settings.Endpoints.Extensions;
using Granit.Templating.Endpoints.Extensions;
using Granit.Timeline.Endpoints.Extensions;
using Granit.Validation.Endpoints.Extensions;
using Granit.Webhooks.Endpoints.Extensions;
using Granit.Workflow.Endpoints.Extensions;

namespace Granit.OpenApi.Generator;

/// <summary>One generated OpenAPI document: a module slug and the routes mounted under it.</summary>
/// <param name="Slug">Document name and <c>GroupName</c> stamped on the module's routes.</param>
/// <param name="Map">Mounts the module's <c>MapGranit*</c> endpoints onto the supplied builder.</param>
internal sealed record EndpointModule(string Slug, Action<IEndpointRouteBuilder> Map);

/// <summary>
/// The single source of truth pairing each <c>Granit.*.Endpoints</c> module with its slug and
/// route-mapping calls. Kept in lockstep with <see cref="GeneratorModule"/>'s dependency graph by
/// <c>OpenApiGeneratorCompletenessTests</c>.
/// </summary>
internal static class GeneratorEndpoints
{
    public static readonly IReadOnlyList<EndpointModule> All =
    [
        new("ai", e => e.MapGranitAI()),
        new("auditing", e => e.MapGranitAuditing()),
        new("api-keys", e => e.MapGranitApiKeys()),
        new("authorization", e => e.MapGranitAuthorization()),
        new("background-jobs", e => e.MapGranitBackgroundJobs()),
        new("bff", e => e.MapGranitBff()),
        new("blob-storage", e => e.MapGranitBlobStorage()),
        new("data-exchange", e => e.MapGranitDataExchange()),
        new("data-lookup", e => e.MapGranitDataLookups()),
        new("diagnostics", e => e.MapGranitDiagnosticsMonitoring()),
        new("features", e => e.MapGranitFeatures()),
        new("hostnames", e => e.MapGranitHostnames()),
        new("cookies", e => e.MapGranitCookieConsent()),
        new("security-headers", e => e.MapGranitSecurityHeadersAudit()),
        new("identity", e => e.MapGranitIdentityUserCache()),
        new("identity-local", e =>
        {
            e.MapGranitAccount();
            e.MapGranitRoles();
        }),
        new("localization", e =>
        {
            e.MapGranitLocalization();
            e.MapGranitLocalizationOverrides();
        }),
        new("multi-tenancy", e => e.MapGranitMultiTenancy()),
        new("notifications", e => e.MapGranitNotifications()),
        new("openiddict", e =>
        {
            e.MapGranitOpenIddict();
            e.MapGranitOpenIddictServer();
        }),
        new("presence", e => e.MapGranitPresence()),
        new("privacy", e =>
        {
            e.MapGranitPrivacy();
            e.MapGranitPrivacyGpcDiscovery();
        }),
        new("scheduling", e => e.MapGranitScheduling()),
        new("settings", e =>
        {
            e.MapGranitUserSettings();
            e.MapGranitGlobalSettings();
            e.MapGranitTenantSettings();
        }),
        new("templating", e => e.MapGranitTemplating()),
        new("timeline", e => e.MapGranitTimeline()),
        new("validation", e => e.MapGranitValidation()),
        new("webhooks", e => e.MapGranitWebhooks()),
        new("workflow", e => e.MapGranitWorkflow()),
    ];
}
