using Granit.Authorization;
using Granit.Hostnames.Endpoints.Internal;
using Granit.Hostnames.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Hostnames.Endpoints;

/// <summary>
/// Granit module for custom hostname management HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes three endpoint groups:
/// <list type="bullet">
/// <item>Managed hostname CRUD — list by owner, get by id, create, delete.</item>
/// <item>Primary flag management — set or clear the canonical hostname flag.</item>
/// <item>Availability pre-check — verify that a hostname is free before registering.</item>
/// </list>
/// Permission definitions are auto-discovered by the authorization module.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHostnamesModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitHostnamesEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<HostnamesEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<HostnamesFeatureProvider>();
    }
}
