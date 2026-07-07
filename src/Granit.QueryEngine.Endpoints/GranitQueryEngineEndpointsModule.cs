using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints.Internal;
using Granit.QueryEngine.Internal;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.QueryEngine.Endpoints;

/// <summary>
/// Granit module for QueryEngine HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes query list routes via <c>MapGranitQuery&lt;TEntity&gt;()</c> plus the
/// <c>GET /meta</c> metadata endpoint. Requires <see cref="GranitQueryEngineModule"/>
/// (core infrastructure) and <see cref="GranitAuthorizationModule"/> (permission
/// policy enforcement). Validators are auto-discovered by <c>GranitValidationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitQueryEngineModule),
    typeof(GranitValidationModule))]
public sealed class GranitQueryEngineEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddLocalizationResource<QueryEngineEndpointsLocalizationResource>();

        // Surfaces every registered IQueryDefinitionDescriptor to the GET /catalog endpoint.
        context.Services.TryAddSingleton<IQueryDefinitionRegistry, QueryDefinitionRegistry>();
    }
}
