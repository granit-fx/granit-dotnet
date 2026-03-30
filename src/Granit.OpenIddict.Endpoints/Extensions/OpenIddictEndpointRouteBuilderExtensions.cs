using Granit.OpenIddict.Endpoints.Endpoints;
using Granit.OpenIddict.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict endpoints.
/// </summary>
public static class OpenIddictEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the OpenIddict account self-service and admin management endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize endpoint options.</param>
    /// <returns>The account <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapOpenIddictEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<OpenIddictEndpointsOptions>? configure = null)
    {
        OpenIddictEndpointsOptions options = new();
        configure?.Invoke(options);

        // ──── Account self-service (/api/account) ────
        RouteGroupBuilder accountGroup = endpoints
            .MapGranitGroup(options.AccountRoutePrefix);

        accountGroup.MapAccountLoginEndpoints();
        accountGroup.MapAccountRegistrationEndpoints();
        accountGroup.MapAccountProfileEndpoints();
        accountGroup.MapAccountPasswordEndpoints();
        accountGroup.MapAccountTwoFactorEndpoints();
        accountGroup.MapAccountExternalLoginEndpoints();
        accountGroup.MapAccountPasskeyEndpoints();
        accountGroup.MapAccountDeletionEndpoints();
        accountGroup.MapAccountSessionEndpoints();

        // ──── Admin management (/api/admin) ────
        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.AdminRoutePrefix)
            .RequireAuthorization();

        adminGroup.MapAdminImpersonationEndpoints();
        adminGroup.MapAdminOidcEndpoints();

        return accountGroup;
    }

    /// <summary>
    /// Maps the OIDC server protocol endpoints (<c>/connect/authorize</c>,
    /// <c>/connect/token</c>, <c>/connect/userinfo</c>, <c>/connect/logout</c>,
    /// <c>/connect/verify</c>).
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
    public static IEndpointRouteBuilder MapOpenIddictServerEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<OpenIddictServerEndpointsOptions>? configure = null)
    {
        OpenIddictServerEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints.MapConnectAuthorizationEndpoints(options);
        endpoints.MapConnectTokenEndpoints();
        endpoints.MapConnectUserInfoEndpoints();
        endpoints.MapConnectLogoutEndpoints(options);
        endpoints.MapConnectVerifyEndpoints();

        return endpoints;
    }
}
