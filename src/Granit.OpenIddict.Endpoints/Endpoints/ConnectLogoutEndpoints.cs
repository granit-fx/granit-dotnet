using System.Diagnostics;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.Endpoints.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Server.AspNetCore;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// OIDC end-session endpoint (<c>GET/POST /connect/logout</c>).
/// Signs out the Identity cookie and lets OpenIddict handle the post-logout redirect.
/// </summary>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignOut with explicit auth scheme
internal static partial class ConnectLogoutEndpoints
{
    internal static IEndpointRouteBuilder MapConnectLogoutEndpoints(
        this IEndpointRouteBuilder endpoints,
        OpenIddictServerEndpointsOptions options)
    {
        endpoints.MapMethods("/connect/logout", ["GET", "POST"],
                (Delegate)((HttpContext context) => HandleLogoutAsync(context, options)))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleLogoutAsync(
        HttpContext context,
        OpenIddictServerEndpointsOptions options)
    {
        // Capture identity before sign-out so the audit row can carry the
        // subject. Once SignOutAsync has run, context.User is anonymous.
        string? userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        string? userName = context.User.Identity?.Name;
        bool wasAuthenticated = context.User.Identity?.IsAuthenticated == true;

        // Sign out the Identity application cookie.
        await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        if (wasAuthenticated)
        {
            ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Granit.OpenIddict.Endpoints.ConnectLogoutEndpoints");
            await TryWriteLogoutAuditAsync(context, logger, userId, userName).ConfigureAwait(false);
        }

        // Let OpenIddict handle post_logout_redirect_uri validation and redirect.
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = options.PostLogoutRedirectPath },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// Records a successful OIDC end-session logout. No-op when
    /// <see cref="IAuditingWriter"/> is not registered; audit failures are
    /// swallowed so logout always completes.
    /// </summary>
    private static async Task TryWriteLogoutAuditAsync(
        HttpContext context,
        ILogger logger,
        string? userId,
        string? userName)
    {
        IAuditingWriter? auditingWriter = context.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = context.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = context.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
        string? userAgent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;

        AuditEntry entry = AuthenticationAuditEntry.CreateSuccess(
            timeProvider.GetUtcNow(),
            userId: string.IsNullOrEmpty(userId) ? AuthenticationAuditEntry.UnknownUserSentinel : userId,
            userName,
            method: "oidc_logout",
            tenantId,
            ipAddress,
            userAgent,
            correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC logout: failed to write authentication audit entry — logout flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
