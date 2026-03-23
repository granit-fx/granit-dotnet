using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;

namespace Granit.DataExchange.Endpoints;

/// <summary>
/// Granit module for data import HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes import management routes via
/// <see cref="Extensions.DataExchangeEndpointRouteBuilderExtensions.MapDataExchangeEndpoints"/>.
/// Requires both <see cref="GranitDataExchangeModule"/> (pipeline infrastructure)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDataExchangeModule),
    typeof(GranitHttpApiDocumentationModule))]
public sealed class GranitDataExchangeEndpointsModule : GranitModule;
