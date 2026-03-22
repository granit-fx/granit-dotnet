using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;

namespace Granit.Bff.Endpoints;

/// <summary>
/// Granit module for BFF authentication HTTP endpoints (login, callback, logout, user, CSRF).
/// </summary>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitHttpApiDocumentationModule))]
public sealed class GranitBffEndpointsModule : GranitModule;
