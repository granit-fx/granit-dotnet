using System.Diagnostics;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF logout endpoint. Clears the session and redirects to the OIDC end_session_endpoint.
/// Registered per-frontend under <c>/{pathPrefix}/bff/logout</c>.
/// </summary>
internal static partial class BffLogoutEndpoints
{
    internal static RouteGroupBuilder MapLogoutEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/logout", (HttpContext httpContext,
                [FromServices] IBffLogoutOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
                HandleLogoutAsync(httpContext, frontend, orchestrator, cancellationToken))
            .WithName($"BffLogout_{frontend.Name}")
            .WithSummary("Logs out the user, clears the session, and redirects to the OIDC end_session_endpoint.")
            .WithDescription(
                "Removes the token set from the distributed cache, deletes the session cookie, "
                + "and redirects to the OIDC provider's end_session_endpoint for RP-Initiated Logout. "
                + "If no session exists, redirects to the post-logout path directly.")
            .Produces(StatusCodes.Status302Found)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler — not a direct endpoint delegate; services are resolved via lambda
    private static async Task<RedirectHttpResult> HandleLogoutAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        IBffLogoutOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Bff.Endpoints.BffLogoutEndpoints");

        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Logout);
        using IDisposable? activityScope = activity;

        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];
        string? idTokenHint = null;
        bool hadActiveSession = !string.IsNullOrEmpty(sessionId);

        if (hadActiveSession)
        {
            idTokenHint = await orchestrator.RevokeSessionAsync(
                frontend.Name, sessionId!, cancellationToken).ConfigureAwait(false);
        }

        // Clear frontend-specific session cookie via managed cookie system (GRSEC004)
        IGranitCookieManager cookieManager = httpContext.RequestServices.GetRequiredService<IGranitCookieManager>();
        cookieManager.DeleteCookie(httpContext, frontend.SessionCookieName);

        if (hadActiveSession)
        {
            await TryWriteLogoutAuditAsync(httpContext, logger,
                userId: null, userName: null, cancellationToken).ConfigureAwait(false);
        }

        // Build end_session URL — support absolute URLs (e.g. separate frontend origin)
        string effectivePath = frontend.EffectivePostLogoutRedirectPath;
        string postLogoutRedirectUri = effectivePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? effectivePath
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{effectivePath}";
        string endSessionUrl = orchestrator.BuildEndSessionUrl(frontend, postLogoutRedirectUri, idTokenHint);

        return TypedResults.Redirect(endSessionUrl);
    }
#pragma warning restore GRAPI003

    /// <summary>
    /// Records a successful BFF session logout as an
    /// <see cref="AuditCategory.PrivilegedAccess"/> audit row. No-op when
    /// <see cref="IAuditingWriter"/> is not registered. Audit failures are
    /// swallowed so logout always completes.
    /// </summary>
    private static async Task TryWriteLogoutAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string? userId,
        string? userName,
        CancellationToken cancellationToken)
    {
        IAuditingWriter? auditingWriter = httpContext.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;

        AuditEntry entry = AuthenticationAuditEntry.CreateSuccess(
            timeProvider.GetUtcNow(),
            userId: string.IsNullOrEmpty(userId) ? AuthenticationAuditEntry.UnknownUserSentinel : userId,
            userName,
            method: "bff_logout",
            tenantId,
            ipAddress,
            userAgent,
            correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF logout: failed to write authentication audit entry — logout flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
