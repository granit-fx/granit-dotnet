using Granit.Authorization;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Timing;
using Granit.Validation;

namespace Granit.QueryEngine.Endpoints;

/// <summary>
/// Granit module for QueryEngine HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes query list routes via
/// <c>MapQueryEndpoints&lt;TEntity, TDto&gt;()</c>,
/// the <c>GET /meta</c> metadata endpoint, and CRUD saved view endpoints.
/// Requires both <see cref="GranitQueryEngineModule"/> (core infrastructure)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Validators are auto-discovered by <c>GranitValidationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitQueryEngineModule),
    typeof(GranitTimingModule),
    typeof(GranitValidationModule))]
public sealed class GranitQueryEngineEndpointsModule : GranitModule;

