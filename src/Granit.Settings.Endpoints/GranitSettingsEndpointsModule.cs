using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Timing;
using Granit.Validation;

namespace Granit.Settings.Endpoints;

/// <summary>
/// Granit module for settings HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes three sets of endpoints:
/// <list type="bullet">
/// <item>User-scoped settings — any authenticated user can read/write their own settings
///   (<see cref="Extensions.UserSettingsEndpointRouteBuilderExtensions.MapGranitUserSettings"/>).</item>
/// <item>Global settings — requires <c>Settings.Global.Read</c>/<c>Settings.Global.Manage</c>
///   (<see cref="Extensions.AdminSettingsEndpointRouteBuilderExtensions.MapGranitGlobalSettings"/>).</item>
/// <item>Tenant settings — requires <c>Settings.Tenant.Read</c>/<c>Settings.Tenant.Manage</c>
///   (<see cref="Extensions.AdminSettingsEndpointRouteBuilderExtensions.MapGranitTenantSettings"/>).</item>
/// </list>
/// Also registers the <see cref="Middleware.SettingsCultureMiddleware"/> setting definitions
/// for locale and timezone.
/// Permission and setting definition providers are auto-discovered by their respective modules.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitSettingsModule),
    typeof(GranitTimingModule),
    typeof(GranitValidationModule))]
public sealed class GranitSettingsEndpointsModule : GranitModule;
