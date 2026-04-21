using Granit.Endpoints;
using Granit.Identity.Local.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

/// <summary>
/// Maps the <c>GET /api/account/config</c> public configuration endpoint.
/// </summary>
internal static class AccountConfigEndpoints
{
    /// <summary>
    /// Maps the Identity.Local module config endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePrefix">Route prefix. Default: <c>"api/account"</c>.</param>
    /// <param name="tag">OpenAPI tag. Default: <c>"Account - Config"</c>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    internal static IEndpointRouteBuilder MapGranitAccountConfig(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "api/account",
        string tag = "Account - Config") =>
        endpoints.MapGranitModuleConfigAsync<IdentityLocalConfigProvider, IdentityLocalConfigResponse>(
            routePrefix,
            "GetAccountConfig",
            tag,
            builder => builder.AllowAnonymous());
}
