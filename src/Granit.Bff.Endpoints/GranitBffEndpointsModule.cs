using Granit.Caching;
using Granit.Http.ApiDocumentation;
using Granit.Http.Cookies;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Bff.Endpoints;

/// <summary>
/// Granit module for BFF authentication HTTP endpoints (login, callback, logout, user, CSRF).
/// </summary>
/// <remarks>
/// Cookie infrastructure is ensured by <see cref="GranitHttpCookiesModule"/> (dependency).
/// BFF session cookies are registered at route-mapping time in <c>MapGranitBff()</c>
/// because their names are configuration-driven (one per frontend).
/// </remarks>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitCachingModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpCookiesModule),
    typeof(GranitValidationModule))]
public sealed class GranitBffEndpointsModule : GranitModule;
