using Granit.Events;
using Granit.Identity;
using Granit.Timing;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Handlers;

/// <summary>
/// OpenIddict server handler that announces a new user session by publishing
/// <see cref="UserSessionCreatedEto"/> when the token endpoint issues a refresh token. This is the OpenIddict
/// equivalent of the BFF's <c>BffLoginEndpoints</c> emission: it lets consumers (anomaly detection, geo
/// enrichment, notifications) react out-of-band to a sign-in without the token request waiting on them.
/// </summary>
/// <remarks>
/// <para>
/// For OpenIddict a "session" <em>is</em> a valid refresh token — <c>OpenIddictSessionManager</c> surfaces it
/// with the token's database id as the session id. That id is stamped onto
/// <see cref="ProcessSignInContext.RefreshTokenPrincipal"/> while the refresh token is generated (built-in
/// order 105_000); this handler is ordered after it (200_000) so the id is available, and after the custom
/// handlers that may reject the sign-in (DPoP, client-side policy) so it never announces a sign-in that was
/// ultimately refused.
/// </para>
/// <para>
/// Only the <em>initial</em> session establishment is announced. <c>refresh_token</c> rotation is skipped: it
/// renews an existing session (and, with rolling tokens, mints a fresh id each time), so re-running risk
/// evaluation on every refresh would be pure noise. Flows that issue no refresh token (client_credentials, or
/// a request without <c>offline_access</c>) carry no session and are skipped naturally.
/// </para>
/// <para>
/// Topology coexistence is handled by the consumer, not here: when a BFF fronts this server the BFF emits its
/// own session-created event, and the unified <c>IUserSessionProvider</c> in that deployment surfaces BFF
/// sessions — not OpenIddict refresh tokens — so the anomaly-detection consumer's ownership guard drops this
/// event. The emitter therefore stays topology-blind and best-effort.
/// </para>
/// </remarks>
public sealed partial class OpenIddictUserSessionCreatedHandler(
    IServiceProvider serviceProvider,
    IClock clock,
    ILogger<OpenIddictUserSessionCreatedHandler> logger)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    private const string TenantIdClaimType = "tenant_id";

    /// <summary>
    /// Descriptor registered with OpenIddict's server pipeline. Runs after refresh-token generation
    /// (order 105_000) so the token id is on <see cref="ProcessSignInContext.RefreshTokenPrincipal"/>, and
    /// after the reject-capable custom handlers (DPoP at 150_000, client-side policy at 100_000).
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<OpenIddictUserSessionCreatedHandler>()
            .SetOrder(200_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Sessions are issued at the token endpoint; the authorization endpoint's sign-in yields a code.
        if (context.EndpointType != OpenIddictServerEndpointType.Token || context.Request is null)
        {
            return;
        }

        // Renewal, not a new session — see the type remarks.
        if (context.Request.IsRefreshTokenGrantType())
        {
            return;
        }

        // No refresh token issued → no session (client_credentials, or no offline_access scope).
        string? sessionId = context.RefreshTokenPrincipal?.GetTokenId();
        string? userId = context.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Best-effort: a no-op when no distributed bus is wired.
        IDistributedEventBus? eventBus = serviceProvider.GetService<IDistributedEventBus>();
        if (eventBus is null)
        {
            return;
        }

        Guid? tenantId = context.Principal?.GetClaim(TenantIdClaimType) is { Length: > 0 } claim
            && Guid.TryParse(claim, out Guid parsed)
                ? parsed
                : null;

        HttpRequest? request = context.Transaction.GetHttpRequest();
        string? ipAddress = request?.HttpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = request?.Headers.UserAgent.FirstOrDefault() is { Length: > 0 } ua ? ua : null;
        string? deviceId = request?.HttpContext is { } httpContext
            ? ResolveTrustedDeviceId(httpContext, userId)
            : null;

        // Announcing the session must never break token issuance — swallow any dispatch fault.
        try
        {
            await eventBus.PublishAsync(
                new UserSessionCreatedEto(
                    userId,
                    sessionId,
                    tenantId,
                    UserSessionSource.OpenIddict,
                    userAgent,
                    ipAddress,
                    clock.Now,
                    deviceId),
                context.CancellationToken).ConfigureAwait(false);

            LogSessionAnnounced(logger, context.Request.GrantType ?? "(null)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAnnounceFailed(logger, ex);
        }
    }

    // Reads the device-trust binding stashed by the device-trust middleware, returning the device id only when
    // it is bound to the same user the session belongs to. Null (untrusted) when the middleware did not run or no
    // valid device cookie was presented (e.g. a back-channel grant with no browser).
    private static string? ResolveTrustedDeviceId(HttpContext httpContext, string userId) =>
        httpContext.Items.TryGetValue(DeviceTrustContextItems.UserId, out object? boundUser)
        && boundUser is string boundUserId
        && string.Equals(boundUserId, userId, StringComparison.Ordinal)
        && httpContext.Items.TryGetValue(DeviceTrustContextItems.DeviceId, out object? deviceId)
            ? deviceId as string
            : null;

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "OpenIddict session-created event published for grant '{GrantType}'.")]
    private static partial void LogSessionAnnounced(ILogger logger, string grantType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenIddict session-created event could not be published — token issuance continues.")]
    private static partial void LogAnnounceFailed(ILogger logger, Exception exception);
}
