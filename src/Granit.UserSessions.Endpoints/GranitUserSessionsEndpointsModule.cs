using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.UserSessions.Endpoints;

/// <summary>
/// Granit module for the canonical user-session HTTP API (<c>/sessions</c>, <c>/devices</c>). Map the routes
/// with <see cref="Extensions.UserSessionEndpointRouteBuilderExtensions.MapGranitUserSessions"/>.
/// </summary>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitUserSessionsModule),
    typeof(GranitValidationModule))]
public sealed class GranitUserSessionsEndpointsModule : GranitModule;
