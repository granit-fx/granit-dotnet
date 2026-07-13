using Granit.Events;
using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Http.RateLimiting.AspNetCore;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyExportEndpoints
{
    internal static RouteGroupBuilder MapPrivacyExportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/exports/scopes", HandleListExportScopesAsync)
             .RequireAuthorization(PrivacyPermissions.Exports.Execute)
             .WithName("ListPrivacyExportScopes")
             .WithSummary("Lists the export scopes visible to the current user.")
             .WithDescription(
                 "Returns one entry per IPrivacyDataProvider the subject can include in an export, after applying "
                 + "the framework visibility gates: module loaded, HasDataAsync probe (Takeout-style "
                 + "\"if you never used it, it doesn't appear\"), and the host IPrivacyScopeVisibilityPolicy. "
                 + "Use the returned ProviderName values to populate the POST /privacy/exports `Scopes` field; "
                 + "unknown / hidden scopes in that POST are silently skipped.")
             .Produces<IReadOnlyList<PrivacyExportScopeResponse>>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/exports/on-behalf-of", HandleRequestExportOnBehalfOfAsync)
             .RequireAuthorization(PrivacyPermissions.Exports.ExecuteOnBehalfOf)
             .RequireGranitRateLimiting(PrivacyExportRateLimitPolicies.ExportCreateOnBehalfOf)
             .WithMetadata(new IdempotentAttribute { Required = false })
             .WithName("RequestPrivacyExportOnBehalfOf")
             .WithSummary("Requests a personal data export on behalf of another data subject (admin DSR).")
             .WithDescription(
                 "Admin-driven counterpart to POST /privacy/exports — gated by the dedicated "
                 + "Privacy.Exports.ExecuteOnBehalfOf permission so RBAC can hand it to a narrow "
                 + "operator role without unlocking it for every authenticated user. The body's "
                 + "SubjectUserId identifies the data subject; the authenticated caller is recorded "
                 + "separately on the audit row so the substitution is fully reconstructable for "
                 + "GDPR Art. 30 ROPA. Uses the dedicated `privacy-export-create-on-behalf-of` "
                 + "rate-limit policy (distinct from the self-service `privacy-export-create`). "
                 + "The subject id is validated against the caller's tenant via "
                 + "IPrivacySubjectValidator — a subject absent from the tenant returns 404 (same "
                 + "status as a non-existent request id, so cross-tenant existence cannot be probed).")
             .Produces<PrivacyExportRequestResponse>(StatusCodes.Status202Accepted)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/exports", HandleRequestExportAsync)
             .RequireAuthorization(PrivacyPermissions.Exports.Execute)
             .RequireGranitRateLimiting(PrivacyExportRateLimitPolicies.ExportCreate)
             .WithMetadata(new IdempotentAttribute { Required = false })
             .WithName("RequestPrivacyExport")
             .WithSummary("Requests a personal data export for the current user.")
             .WithDescription(
                 "Triggers the scatter-gather export saga (GDPR Art. 15/20). Each registered data provider "
                 + "prepares its fragment asynchronously. The saga assembles fragments into a downloadable archive. "
                 + "The optional `Scopes` array narrows the export to a subset of provider scopes (see "
                 + "GET /privacy/exports/scopes); omitting it exports everything visible to the subject. "
                 + "Poll GET /exports/{requestId} for status, or wire a Granit.Notifications handler on "
                 + "ExportCompletedEto to notify the user when the archive is ready. "
                 + "Rate-limited via the `privacy-export-create` policy (hosts configure quotas in "
                 + "`RateLimiting:Policies` — defaults to 1 export per subject per 24 hours). "
                 + "Honours an optional `Idempotency-Key` header (Granit.Http.Idempotency) so accidental "
                 + "double-clicks don't spawn two scatter-gather sagas.")
             .Produces<PrivacyExportRequestResponse>(StatusCodes.Status202Accepted)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/exports/{requestId:guid}", HandleGetExportStatusAsync)
             .WithName("GetPrivacyExportStatus")
             .WithSummary("Returns the status of a personal data export request.")
             .WithDescription(
                 "Queries the export request tracker for the specified request ID. "
                 + "Only the user who created the request can view its status. "
                 + "Returns the current state (Pending, Completed, PartiallyCompleted, TimedOut), "
                 + "the archive blob reference when available, and any missing providers. "
                 + "Returns 404 if the request ID is not found or belongs to another user.")
             .Produces<PrivacyExportStatusResponse>()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/exports", HandleGetMyExportsAsync)
             .WithName("ListPrivacyExports")
             .WithSummary("Lists all export requests for the current user.")
             .WithDescription(
                 "Returns all export requests submitted by the current user, ordered by most recent first. "
                 + "Each entry includes the request state, timestamps, and archive reference when available.")
             .Produces<IReadOnlyList<PrivacyExportStatusResponse>>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPrivacyExportDownloadEndpoints();

        return group;
    }

    private static async Task<Results<Accepted<PrivacyExportRequestResponse>, ProblemHttpResult>> HandleRequestExportAsync(
        [FromBody] PrivacyExportRequest? body,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IExportRequestTrackerWriter tracker,
        [FromServices] IPrivacyExportAuditWriter auditWriter,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] TimeProvider timeProvider,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        // Null / empty Scopes → "everything visible" (Takeout default). Unknown / hidden
        // entries are silently dropped by the saga via the visibility resolver.
        IReadOnlyList<string>? requestedScopes = body?.Scopes is { Count: > 0 } scopes ? scopes : null;

        // Self-service: caller == subject. The tracker collapses equal pair to a null
        // CallerUserId so the row is indistinguishable from pre-CallerUserId rows.
        await tracker
            .RecordRequestAsync(requestId, userId, userId, now, cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new PersonalDataRequestedEto(
                    RequestId: requestId,
                    UserId: userId,
                    RequestedAt: now,
                    Regulation: regulation,
                    TenantId: tenantId,
                    RequestedScopes: requestedScopes),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordExportRequested(tenantId, regulation);

        await auditWriter.WriteExportRequestedAsync(
            new PrivacyExportRequestedAudit(
                RequestId: requestId,
                CallerUserId: userId,
                SubjectUserId: userId,
                TenantId: tenantId,
                Regulation: regulation,
                ResolvedScopes: requestedScopes ?? [],
                ClientIp: PrivacyResponseMapper.PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                CorrelationId: httpContext.TraceIdentifier,
                Timestamp: now),
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted(
            $"/privacy/export/{requestId}",
            new PrivacyExportRequestResponse(requestId, now));
    }

    private static async Task<Results<Accepted<PrivacyExportRequestResponse>, ProblemHttpResult>> HandleRequestExportOnBehalfOfAsync(
        [FromBody] PrivacyExportOnBehalfOfRequest body,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IExportRequestTrackerWriter tracker,
        [FromServices] IPrivacyExportAuditWriter auditWriter,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] TimeProvider timeProvider,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        [FromServices] IPrivacySubjectValidator subjectValidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid callerUserId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        // SubjectUserId non-empty + scope bounds are enforced by
        // PrivacyExportOnBehalfOfRequestValidator at the FluentValidation auto-filter
        // pass — the handler is only reached on a clean body.

        // Tenant-bound subject existence check. A subject the caller cannot see in
        // its current tenant returns 404 (NOT 403) — same status as a non-existent
        // request id, so cross-tenant subject probing yields no signal.
        bool subjectExists = await subjectValidator
            .SubjectExistsInCurrentTenantAsync(body.SubjectUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!subjectExists)
        {
            return TypedResults.Problem(
                detail: $"Subject '{body.SubjectUserId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string>? requestedScopes = body.Scopes is { Count: > 0 } scopes ? scopes : null;

        // The saga's PersonalDataRequestedEto.UserId names the data subject — providers
        // and the visibility resolver key off it for HasData probes etc. The caller's
        // identity is persisted both on the tracker row (so the admin sees the export
        // on their listing) and on the audit row (Art. 30 ROPA).
        await tracker
            .RecordRequestAsync(requestId, body.SubjectUserId, callerUserId, now, cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new PersonalDataRequestedEto(
                    RequestId: requestId,
                    UserId: body.SubjectUserId,
                    RequestedAt: now,
                    Regulation: regulation,
                    TenantId: tenantId,
                    RequestedScopes: requestedScopes),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordExportRequested(tenantId, regulation);

        await auditWriter.WriteExportRequestedAsync(
            new PrivacyExportRequestedAudit(
                RequestId: requestId,
                CallerUserId: callerUserId,
                SubjectUserId: body.SubjectUserId,
                TenantId: tenantId,
                Regulation: regulation,
                ResolvedScopes: requestedScopes ?? [],
                ClientIp: PrivacyResponseMapper.PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                CorrelationId: httpContext.TraceIdentifier,
                Timestamp: now),
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Accepted(
            $"/privacy/export/{requestId}",
            new PrivacyExportRequestResponse(requestId, now));
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyExportScopeResponse>>, ProblemHttpResult>> HandleListExportScopesAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IPrivacyScopeResolver scopeResolver,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        Guid? tenantGuid = currentTenant.IsAvailable ? currentTenant.Id : null;
        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        // RequestId is synthetic here — we're not actually starting an export, just probing
        // visibility. Subject == Caller (self-service); admin "on behalf of" flows are deferred.
        PrivacyExportContext probeContext = new(
            RequestId: guidGenerator.Create(),
            SubjectUserId: userId,
            CallerUserId: userId,
            TenantId: tenantGuid,
            Regulation: regulation);

        IReadOnlyList<ProviderDescriptor> visible = await scopeResolver
            .ListVisibleAsync(probeContext, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyExportScopeResponse> response = [.. visible.Select(d => new PrivacyExportScopeResponse(
            ProviderName: d.ProviderName,
            DisplayKey: d.DisplayKey,
            FeatureName: d.FeatureName,
            DefaultSelected: d.DefaultSelected,
            EstimatedSizeBytes: d.EstimatedSizeBytes))];

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PrivacyExportStatusResponse>, ProblemHttpResult>> HandleGetExportStatusAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        ExportRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        // Caller can see status when they are the subject OR the operator who triggered
        // the admin DSR — keeps the operational visibility loop closed for support teams
        // without giving them a download path (that stays subject-only).
        if (status is null || (status.SubjectUserId != userId && status.CallerUserId != userId))
        {
            return TypedResults.Problem(
                detail: $"Export request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(PrivacyResponseMapper.MapExportStatus(status));
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyExportStatusResponse>>, ProblemHttpResult>> HandleGetMyExportsAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        IReadOnlyList<ExportRequestStatus> statuses = await tracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyExportStatusResponse> result = statuses
            .Select(PrivacyResponseMapper.MapExportStatus)
            .ToList();

        return TypedResults.Ok(result);
    }
}
