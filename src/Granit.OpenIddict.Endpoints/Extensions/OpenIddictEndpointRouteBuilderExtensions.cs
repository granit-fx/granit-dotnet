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

        adminGroup.MapAdminUserEndpoints();
        adminGroup.MapAdminRoleEndpoints();
        adminGroup.MapAdminGroupEndpoints();
        adminGroup.MapAdminOidcEndpoints();

        return accountGroup;
    }
}
