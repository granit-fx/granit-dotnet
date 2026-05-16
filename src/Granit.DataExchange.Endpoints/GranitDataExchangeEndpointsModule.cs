using Granit.Authorization;
using Granit.DataExchange.Endpoints.Internal;
using Granit.DataExchange.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.DataExchange.Endpoints;

/// <summary>
/// Granit module for data import HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes import management routes via
/// <see cref="Extensions.DataExchangeEndpointRouteBuilderExtensions.MapGranitDataExchange"/>.
/// Requires both <see cref="GranitDataExchangeModule"/> (pipeline infrastructure)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDataExchangeModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitDataExchangeEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<DataExchangeEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<DataExchangeFeatureProvider>();
    }
}
