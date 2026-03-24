using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Bff.Endpoints;

/// <summary>
/// Granit module for BFF authentication HTTP endpoints (login, callback, logout, user, CSRF).
/// </summary>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitBffEndpointsModule : GranitModule;
