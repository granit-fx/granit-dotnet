using System.Diagnostics;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Http.Idempotency;
using Granit.Http.SecurityHeaders.Extensions;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Endpoints.Permissions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static partial class AdminImpersonationEndpoints
{
    internal static RouteGroupBuilder MapAdminImpersonationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/users/{userId:guid}/impersonate", ImpersonateAsync)
            .WithName("ImpersonateUser")
            .WithSummary("Impersonates a user.")
            .WithDescription(
                "Issues a short-lived token (max 1h) with impersonator_id claim. "
                + "Writes audit log and sends transparency notification.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<ImpersonationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(IdentityLocalPermissions.Users.Impersonate)
            .WithNoStoreResponse();

        return group;
    }

    private static async Task<Results<Ok<ImpersonationResponse>, ProblemHttpResult>> ImpersonateAsync(
        Guid userId,
        HttpContext httpContext,
        [FromServices] IImpersonationService impersonationService,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.Impersonation);

        // Guard: cannot chain-impersonate
        if (httpContext.User.FindFirst("impersonator_id") is not null)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Impersonation:AlreadyImpersonating", "Cannot impersonate while already impersonating."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        string adminId = httpContext.User.FindFirst("sub")!.Value;
        string adminName = httpContext.User.FindFirst("email")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value
                           ?? adminId;

        ImpersonationResult result = await impersonationService
            .ImpersonateAsync(userId.ToString(), adminId, adminName, cancellationToken)
            .ConfigureAwait(false);

        // Durable audit record (the compliance source of truth); the transparency notification to the
        // impersonated user derives from it via AuditEntryPersistedEto (see the .Notifications handler).
        await TryWriteImpersonationAuditAsync(httpContext, userId, adminId, adminName, cancellationToken)
            .ConfigureAwait(false);

        string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        httpContext.RequestServices.GetService<IdentityLocalMetrics>()?.RecordImpersonation(tenantId);
        return TypedResults.Ok(IdentityLocalResponseMapper.ToResponse(result));
    }

    /// <summary>
    /// Records a <see cref="AuditCategory.PrivilegedAccess"/> audit entry for the impersonation
    /// (ISO 27001 A.12.4.3). No-op when <see cref="IAuditingWriter"/> is not registered; audit
    /// failures are swallowed so a transient audit-store outage never breaks impersonation. The
    /// impersonated user is the synthetic change's <c>EntityId</c> and the impersonator is the
    /// entry's actor — the notification handler reads both back from the persisted entry.
    /// </summary>
    private static async Task TryWriteImpersonationAuditAsync(
        HttpContext httpContext,
        Guid targetUserId,
        string impersonatorId,
        string impersonatorName,
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
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();

        AuditEntry entry = ImpersonationAuditEntry.Create(
            timeProvider.GetUtcNow(),
            impersonatorId,
            impersonatorName,
            targetUserId,
            tenantId,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            Activity.Current?.Id);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ILogger? logger = httpContext.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(AdminImpersonationEndpoints).FullName!);
            if (logger is not null)
            {
                LogAuditWriteFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write impersonation audit entry.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
