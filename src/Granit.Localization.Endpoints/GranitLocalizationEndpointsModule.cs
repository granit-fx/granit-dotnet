using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Endpoints.Workspaces;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Localization.Endpoints;

/// <summary>
/// Granit module for localization HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes two sets of endpoints:
/// <list type="bullet">
/// <item><c>GET /api/{version}/localization</c> — anonymous SPA bootstrapping, via
///   <see cref="Extensions.LocalizationEndpointRouteBuilderExtensions.MapGranitLocalization"/>.</item>
/// <item>CRUD <c>/api/{version}/localization/overrides</c> — admin override management
///   (requires <c>Localization.Overrides.Manage</c> permission), via
///   <see cref="Extensions.LocalizationEndpointRouteBuilderExtensions.MapGranitLocalizationOverrides"/>.</item>
/// </list>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitLocalizationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitLocalizationEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddWorkspaceContribution<LocalizationWorkspaceContribution>();
        context.Services.AddFeatureProvider<LocalizationFeatureProvider>();
    }
}
