using Granit.Modularity;

namespace Granit.OpenApi.Generator;

/// <summary>
/// Root module for the OpenAPI contract-artifact generator. Its <see cref="DependsOnAttribute"/>
/// graph pulls in every <c>Granit.*.Endpoints</c> module so the Granit module system configures
/// their services; <see cref="GeneratorEndpoints"/> mounts the matching routes.
/// </summary>
/// <remarks>
/// Keep this list and <see cref="GeneratorEndpoints.All"/> in lockstep with the set of
/// <c>src/Granit.*.Endpoints</c> packages — <c>OpenApiGeneratorCompletenessTests</c> fails the
/// build if a module is added without being wired here. A small number of HTTP surfaces ship
/// from a non-<c>.Endpoints</c> package (e.g. the query catalogue in
/// <c>Granit.QueryEngine.AspNetCore</c>); those are wired explicitly and tracked by the same test.
/// </remarks>
[DependsOn(
    typeof(Granit.AI.Chat.Endpoints.GranitAIChatEndpointsModule),
    typeof(Granit.AI.Endpoints.GranitAIEndpointsModule),
    typeof(Granit.AI.Prompts.Endpoints.GranitAIPromptsEndpointsModule),
    typeof(Granit.Auditing.Endpoints.GranitAuditingEndpointsModule),
    typeof(Granit.Authentication.ApiKeys.Endpoints.GranitAuthenticationApiKeysEndpointsModule),
    typeof(Granit.Authorization.Endpoints.GranitAuthorizationEndpointsModule),
    typeof(Granit.BackgroundJobs.Endpoints.GranitBackgroundJobsEndpointsModule),
    typeof(Granit.Bff.Endpoints.GranitBffEndpointsModule),
    typeof(Granit.BlobStorage.Endpoints.GranitBlobStorageEndpointsModule),
    typeof(Granit.DataExchange.Endpoints.GranitDataExchangeEndpointsModule),
    typeof(Granit.DataLookup.Endpoints.GranitDataLookupEndpointsModule),
    typeof(Granit.Diagnostics.Endpoints.GranitDiagnosticsEndpointsModule),
    typeof(Granit.Features.Endpoints.GranitFeaturesEndpointsModule),
    typeof(Granit.Geocoding.Endpoints.GranitGeocodingEndpointsModule),
    typeof(Granit.Hostnames.Endpoints.GranitHostnamesEndpointsModule),
    typeof(Granit.Http.Cookies.Endpoints.GranitHttpCookiesEndpointsModule),
    typeof(Granit.Http.SecurityHeaders.Endpoints.GranitHttpSecurityHeadersEndpointsModule),
    typeof(Granit.Identity.Endpoints.GranitIdentityEndpointsModule),
    typeof(Granit.Identity.Local.Endpoints.GranitIdentityLocalEndpointsModule),
    typeof(Granit.Localization.Endpoints.GranitLocalizationEndpointsModule),
    typeof(Granit.MultiTenancy.Endpoints.GranitMultiTenancyEndpointsModule),
    typeof(Granit.Notifications.Endpoints.GranitNotificationsEndpointsModule),
    typeof(Granit.OpenIddict.Endpoints.GranitOpenIddictEndpointsModule),
    typeof(Granit.Presence.Endpoints.GranitPresenceEndpointsModule),
    typeof(Granit.Privacy.Endpoints.GranitPrivacyEndpointsModule),
    typeof(Granit.QueryEngine.AspNetCore.GranitQueryEngineAspNetCoreModule),
    typeof(Granit.Scheduling.Endpoints.GranitSchedulingEndpointsModule),
    typeof(Granit.Settings.Endpoints.GranitSettingsEndpointsModule),
    typeof(Granit.Templating.Endpoints.GranitTemplatingEndpointsModule),
    typeof(Granit.Timeline.Endpoints.GranitTimelineEndpointsModule),
    typeof(Granit.Validation.Endpoints.GranitValidationEndpointsModule),
    typeof(Granit.Webhooks.Endpoints.GranitWebhooksEndpointsModule),
    typeof(Granit.Workflow.Endpoints.GranitWorkflowEndpointsModule))]
public sealed class GeneratorModule : GranitModule;
