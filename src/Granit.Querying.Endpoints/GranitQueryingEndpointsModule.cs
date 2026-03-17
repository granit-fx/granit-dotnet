using Granit.Authorization;
using Granit.Core.Modularity;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Timing;
using Granit.Validation;

namespace Granit.Querying.Endpoints;

/// <summary>
/// Granit module for querying HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes query list routes via
/// <c>MapQueryEndpoints&lt;TEntity, TDto&gt;()</c>,
/// the <c>GET /meta</c> metadata endpoint, and CRUD saved view endpoints.
/// Requires both <see cref="GranitQueryingModule"/> (core infrastructure)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Validators are auto-discovered by <c>GranitValidationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitQueryingModule),
    typeof(GranitTimingModule),
    typeof(GranitValidationModule))]
public sealed class GranitQueryingEndpointsModule : GranitModule;

