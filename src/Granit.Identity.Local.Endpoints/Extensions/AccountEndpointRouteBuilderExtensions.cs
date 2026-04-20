using Granit.Identity.Local.Endpoints.Endpoints;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering account self-service endpoints.
/// </summary>
public static class AccountEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the account self-service and admin impersonation endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize endpoint options.</param>
    /// <returns>The account <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitAccount(
        this IEndpointRouteBuilder endpoints,
        Action<AccountEndpointsOptions>? configure = null)
    {
        AccountEndpointsOptions options = new();
        configure?.Invoke(options);

        // ──── Account self-service (/api/account) ────
        RouteGroupBuilder accountGroup = endpoints
            .MapGranitGroup(options.AccountRoutePrefix)
            .WithTags(options.AccountTagName);

        accountGroup.MapAccountLoginEndpoints();
        accountGroup.MapAccountRegistrationEndpoints();
        accountGroup.MapAccountProfileEndpoints();
        accountGroup.MapAccountEmailChangeEndpoints();
        accountGroup.MapAccountPasswordEndpoints();
        accountGroup.MapAccountTwoFactorEndpoints();
        accountGroup.MapAccountExternalLoginEndpoints();
        accountGroup.MapAccountPasskeyEndpoints();
        accountGroup.MapAccountDeletionEndpoints();
        accountGroup.MapAccountSessionEndpoints();

        // ──── Public config (/api/account/config) ────
        endpoints.MapGranitAccountConfig(options.AccountRoutePrefix);

        // ──── Admin management (/api/admin) ────
        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.AdminRoutePrefix)
            .WithTags(options.AdminTagName)
            .RequireAuthorization();

        adminGroup.MapAdminImpersonationEndpoints();

        return accountGroup;
    }
}
