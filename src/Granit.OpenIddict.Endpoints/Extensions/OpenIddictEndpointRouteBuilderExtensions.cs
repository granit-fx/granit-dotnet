using Granit.OpenIddict.Endpoints.Endpoints;
using Granit.OpenIddict.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict endpoints.
/// </summary>
public static class OpenIddictEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the OpenIddict admin management endpoints (OIDC application/scope/authorization CRUD).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Account self-service endpoints (login, registration, profile, etc.) have moved to
    /// <c>Granit.Identity.Local.Endpoints</c>. Use <c>MapGranitAccount()</c> from that
    /// module instead.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize endpoint options.</param>
    /// <returns>The admin <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitOpenIddict(
        this IEndpointRouteBuilder endpoints,
        Action<OpenIddictEndpointsOptions>? configure = null)
    {
        OpenIddictEndpointsOptions options = new();
        configure?.Invoke(options);

        // ──── Admin OIDC management (/api/admin) ────
        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.AdminRoutePrefix)
            .WithTags(options.AdminTagName)
            .RequireAuthorization();

        adminGroup.MapAdminOidcEndpoints();

        return adminGroup;
    }

    /// <summary>
    /// Maps the OIDC server protocol endpoints (<c>/connect/authorize</c>,
    /// <c>/connect/token</c>, <c>/connect/userinfo</c>, <c>/connect/logout</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are OpenIddict passthrough handlers — the OpenIddict middleware validates
    /// protocol parameters (client authentication, scopes, PKCE, redirect URIs) and
    /// passes through to these handlers for business logic (user authentication,
    /// consent, token issuance).
    /// </para>
    /// <para>
    /// Routes are mapped at the root path (<c>/connect/*</c>) without API versioning
    /// or group prefix. They are excluded from OpenAPI documentation.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="OpenIddictServerEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitOpenIddictServer(
        this IEndpointRouteBuilder endpoints,
        Action<OpenIddictServerEndpointsOptions>? configure = null)
    {
        OpenIddictServerEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints.MapConnectAuthorizationEndpoints(options);
        endpoints.MapConnectTokenEndpoints();
        endpoints.MapConnectUserInfoEndpoints();
        endpoints.MapConnectLogoutEndpoints(options);

        return endpoints;
    }
}
