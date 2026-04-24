using Granit.MultiTenancy;
using Granit.OpenIddict.Extensions;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Handlers;

/// <summary>
/// OpenIddict server handler that enforces the <see cref="MultiTenancySide"/> policy
/// declared on an OIDC application. Rejects sign-in when the authenticated user's
/// tenancy (host or tenant, as carried by the <c>tenant_id</c> claim) does not
/// match the application's declared side.
/// </summary>
/// <remarks>
/// <para>
/// The rule is deliberately scoped to user sign-ins: <c>client_credentials</c>
/// requests carry no user principal and are left untouched, preserving
/// service-to-service flows regardless of the application's declared side.
/// </para>
/// <para>
/// Policy semantics:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="MultiTenancySide.Host"/> — only users with
/// <c>TenantId = null</c> (no <c>tenant_id</c> claim) may obtain tokens.</description></item>
/// <item><description><see cref="MultiTenancySide.Tenant"/> — only users with a
/// non-empty <c>tenant_id</c> claim may obtain tokens.</description></item>
/// <item><description><see cref="MultiTenancySide.Both"/> or <see langword="null"/> —
/// no restriction (backward-compatible default).</description></item>
/// </list>
/// <para>
/// On mismatch the handler rejects the sign-in with <c>access_denied</c>, which
/// OpenIddict surfaces to the caller as a standard OIDC error response.
/// </para>
/// </remarks>
public sealed partial class ClientSideAuthorizationHandler(
    IOpenIddictApplicationManager applicationManager,
    ILogger<ClientSideAuthorizationHandler> logger)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    /// <summary>
    /// Well-known claim type for the tenant identifier carried on the principal,
    /// as populated by <c>GranitUserClaimsPrincipalFactory</c> for tenant users.
    /// </summary>
    private const string TenantIdClaimType = "tenant_id";

    /// <summary>
    /// Descriptor registered with OpenIddict's server pipeline. Runs as a scoped
    /// handler (because it resolves <see cref="IOpenIddictApplicationManager"/>)
    /// and is ordered late enough to see the resolved <c>ClientId</c> on
    /// <see cref="BaseContext"/>.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<ClientSideAuthorizationHandler>()
            .SetOrder(100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Service-to-service flows carry no user — let them through unchanged.
        if (context.Request is not null && context.Request.IsClientCredentialsGrantType())
        {
            return;
        }

        string? clientId = context.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            return;
        }

        object? application = await applicationManager
            .FindByClientIdAsync(clientId, context.CancellationToken)
            .ConfigureAwait(false);

        if (application is null)
        {
            // Built-in handlers reject unknown clients before we run — defensive skip.
            return;
        }

        MultiTenancySide? clientSide = await applicationManager
            .GetClientSideAsync(application, context.CancellationToken)
            .ConfigureAwait(false);

        if (clientSide is null || clientSide == MultiTenancySide.Both)
        {
            return;
        }

        bool userIsTenant = HasTenantIdClaim(context);

        bool policyViolated = clientSide == MultiTenancySide.Host
            ? userIsTenant
            : !userIsTenant;

        if (!policyViolated)
        {
            return;
        }

        LogClientSideViolation(logger, clientId, clientSide.Value.ToString(),
            userIsTenant ? "tenant" : "host");

        context.Reject(
            error: OpenIddictConstants.Errors.AccessDenied,
            description: "The authenticated user is not authorised for this client's side.");
    }

    private static bool HasTenantIdClaim(ProcessSignInContext context)
    {
        System.Security.Claims.Claim? claim = context.Principal?.FindFirst(TenantIdClaimType);
        return claim is not null && !string.IsNullOrEmpty(claim.Value);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenIddict client-side policy violation: client '{ClientId}' declared ClientSide={ClientSide}, user is {UserSide} — rejecting sign-in.")]
    private static partial void LogClientSideViolation(
        ILogger logger, string clientId, string clientSide, string userSide);
}
