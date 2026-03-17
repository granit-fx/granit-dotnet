using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;

namespace Granit.Authorization.Endpoints;

/// <summary>
/// Granit module for authorization management HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes permission management routes via
/// <see cref="Extensions.AuthorizationEndpointRouteBuilderExtensions.MapAuthorizationEndpoints"/>.
/// Requires <see cref="GranitAuthorizationModule"/> for permission policy enforcement.
/// The application host must register an implementation of
/// <see cref="Abstractions.IPermissionManagerReader"/>/<see cref="Abstractions.IPermissionManagerWriter"/>
/// (e.g. via <c>[DependsOn(typeof(GranitAuthorizationEntityFrameworkCoreModule))]</c>).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule))]
public sealed class GranitAuthorizationEndpointsModule : GranitModule;
