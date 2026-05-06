using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Taxonomy.Endpoints;

/// <summary>
/// Granit module for the Taxonomy HTTP endpoints.
/// </summary>
/// <remarks>
/// Phase T2.1 surfaces Tag CRUD and per-scope autocomplete; assignment endpoints
/// (T2.2), cross-entity search (T3.1), and Category endpoints (T4) ship in
/// subsequent stories of the Granit.Taxonomy Epic.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitTaxonomyModule),
    typeof(GranitValidationModule))]
public sealed class GranitTaxonomyEndpointsModule : GranitModule;
