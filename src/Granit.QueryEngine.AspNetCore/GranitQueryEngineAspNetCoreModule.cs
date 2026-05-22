using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.AspNetCore.Internal;
using Granit.Validation;

namespace Granit.QueryEngine.AspNetCore;

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
public sealed class GranitQueryEngineAspNetCoreModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddLocalizationResource<QueryEngineAspNetCoreLocalizationResource>();
    }
}
