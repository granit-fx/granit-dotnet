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
    /// <remarks>
    /// Endpoints are split into functional tag groups (Login, Registration, Profile,
    /// Email Change, Password, Two-Factor, External Logins, Passkeys, Deletion,
    /// Session, Config) so the Scalar UI exposes each self-service feature as its
    /// own collapsible section. The admin-start and user-end impersonation endpoints
    /// share a single <c>"Impersonation"</c> tag even though they live on different
    /// URL roots, so the feature's full surface is visible at a glance.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize endpoint options.</param>
    /// <returns>The account <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitAccount(
        this IEndpointRouteBuilder endpoints,
        Action<AccountEndpointsOptions>? configure = null)
    {
        AccountEndpointsOptions options = new();
        configure?.Invoke(options);

        // ──── Account self-service (/api/account) — split into feature sub-tags ────
        // Each MapGroup(string.Empty) below adds no route prefix; its only purpose is to attach a
        // distinct OpenAPI tag. The auto-validation filter from MapGranitGroup propagates to the
        // children, so MapGranitGroup is not needed at this level.
        RouteGroupBuilder accountGroup = endpoints.MapGranitGroup(options.AccountRoutePrefix);

        accountGroup.MapGroup(string.Empty).WithTags(options.LoginTagName).MapAccountLoginEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.RegistrationTagName).MapAccountRegistrationEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.ProfileTagName).MapAccountProfileEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.EmailChangeTagName).MapAccountEmailChangeEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.PasswordTagName).MapAccountPasswordEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.TwoFactorTagName).MapAccountTwoFactorEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.ExternalLoginsTagName).MapAccountExternalLoginEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.PasskeysTagName).MapAccountPasskeyEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.DeletionTagName).MapAccountDeletionEndpoints();
        accountGroup.MapGroup(string.Empty).WithTags(options.SessionTagName).MapAccountSessionHeartbeatEndpoint();

        // ──── Impersonation — shared tag across admin-start (/admin/users/{id}/impersonate)
        //      and user-end (/account/session/back-to-impersonator) ────
        accountGroup.MapGroup(string.Empty)
            .WithTags(options.ImpersonationTagName)
            .MapAccountBackToImpersonatorEndpoint();

        // ──── Public config (/api/account/config) ────
        endpoints.MapGranitAccountConfig(options.AccountRoutePrefix, options.ConfigTagName);

        // ──── Admin management (/api/admin) ────
        // Each endpoint declares its own permission via RequireAuthorization(IdentityLocalPermissions.Users.*),
        // so a parent-level RequireAuthorization() would be redundant.
        RouteGroupBuilder adminGroup = endpoints
            .MapGranitGroup(options.AdminRoutePrefix)
            .WithTags(options.ImpersonationTagName);

        adminGroup.MapAdminImpersonationEndpoints();

        return accountGroup;
    }
}
