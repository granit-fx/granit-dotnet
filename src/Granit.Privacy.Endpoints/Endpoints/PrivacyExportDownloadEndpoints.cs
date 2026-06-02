using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Exceptions;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Options;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints.Endpoints;

/// <summary>
/// Privacy-export download routes — manifest + per-shard streaming with
/// step-up authentication gating. Companion to the request/status routes
/// declared in <c>PrivacyEndpointRouteBuilderExtensions.MapExportEndpoints</c>.
/// </summary>
/// <remarks>
/// <para>
/// The download path streams bytes through the BFF (no presigned-URL 302)
/// because <see cref="IPrivacyExportDownloadResolver"/> reaches into the raw
/// <c>IBlobStoreProvider</c> to resolve shard archives — they have no
/// <c>IBlobStorage</c> descriptor entry of their own, only the manifest does.
/// Streaming also keeps every byte under the step-up gate and the ROPA audit
/// trail, which a direct cloud-to-client transfer would bypass.
/// </para>
/// </remarks>
internal static class PrivacyExportDownloadEndpoints
{
    internal static RouteGroupBuilder MapPrivacyExportDownloadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/exports/{requestId:guid}/download", HandleDownloadAsync)
            .RequireAuthorization(PrivacyPermissions.Exports.Execute)
            .WithName("DownloadPrivacyExport")
            .WithSummary("Downloads the personal data export archive (compat — single-shard or manifest).")
            .WithDescription(
                "Convenience route that resolves to shard 0 when the export produced a single archive, "
                + "or to the manifest sidecar when sharded into multiple ZIPs. For deterministic multi-shard "
                + "downloads, use GET /exports/{requestId}/download/{shardIndex} per shard plus "
                + "GET /exports/{requestId}/download/manifest to discover the shard count. "
                + "Step-up authentication required when DownloadStepUpRequired is enabled (default).")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/exports/{requestId:guid}/download/manifest", HandleDownloadManifestAsync)
            .RequireAuthorization(PrivacyPermissions.Exports.Execute)
            .WithName("DownloadPrivacyExportManifest")
            .WithSummary("Downloads the manifest sidecar describing the export's shards.")
            .WithDescription(
                "Returns the signed JSON manifest produced by the assembly job. Clients use it to discover "
                + "the shard count and per-shard sha256/integrity tag before pulling each ZIP. "
                + "Step-up authentication required when DownloadStepUpRequired is enabled (default).")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/exports/{requestId:guid}/download/{shardIndex:int:min(0)}", HandleDownloadShardAsync)
            .RequireAuthorization(PrivacyPermissions.Exports.Execute)
            .WithName("DownloadPrivacyExportShard")
            .WithSummary("Downloads a single shard of a sharded personal data export.")
            .WithDescription(
                "Streams the shard ZIP indexed by 0..(ShardCount-1) from the export's archive set. "
                + "Shard indices outside the manifest's bounds return 404. "
                + "Step-up authentication required when DownloadStepUpRequired is enabled (default).")
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> HandleDownloadAsync(
        Guid requestId,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        [FromServices] IPrivacyExportDownloadResolver downloadResolver,
        [FromServices] IPrivacyExportAuditWriter auditWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<PrivacyEndpointsOptions> options,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        ExportRequestStatus? status = await tracker.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
        ProblemHttpResult? gate = Gate(httpContext, options.Value, timeProvider, status, requestId, userId);
        if (gate is not null)
        {
            return gate;
        }

        try
        {
            PrivacyExportManifestSummary summary = await downloadResolver
                .ReadManifestSummaryAsync(requestId, cancellationToken).ConfigureAwait(false);

            (PrivacyExportDownloadPayload payload, int auditedShardIndex) = summary.ShardCount switch
            {
                1 => (await downloadResolver.OpenShardAsync(requestId, 0, cancellationToken).ConfigureAwait(false), 0),
                _ => (await downloadResolver.OpenManifestAsync(requestId, cancellationToken).ConfigureAwait(false), ManifestShardSentinel),
            };

            await WriteShardDownloadAuditAsync(auditWriter, httpContext, currentTenant, requestId, userId, status!.SubjectUserId, auditedShardIndex, timeProvider, cancellationToken).ConfigureAwait(false);
            return ToFileResult(payload);
        }
        catch (PrivacyExportNotReadyException)
        {
            return ExportNotReady(requestId);
        }
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> HandleDownloadManifestAsync(
        Guid requestId,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        [FromServices] IPrivacyExportDownloadResolver downloadResolver,
        [FromServices] IPrivacyExportAuditWriter auditWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<PrivacyEndpointsOptions> options,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        ExportRequestStatus? status = await tracker.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
        ProblemHttpResult? gate = Gate(httpContext, options.Value, timeProvider, status, requestId, userId);
        if (gate is not null)
        {
            return gate;
        }

        try
        {
            PrivacyExportDownloadPayload payload = await downloadResolver
                .OpenManifestAsync(requestId, cancellationToken).ConfigureAwait(false);
            await WriteShardDownloadAuditAsync(auditWriter, httpContext, currentTenant, requestId, userId, status!.SubjectUserId, ManifestShardSentinel, timeProvider, cancellationToken).ConfigureAwait(false);
            return ToFileResult(payload);
        }
        catch (PrivacyExportNotReadyException)
        {
            return ExportNotReady(requestId);
        }
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> HandleDownloadShardAsync(
        Guid requestId,
        int shardIndex,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        [FromServices] IPrivacyExportDownloadResolver downloadResolver,
        [FromServices] IPrivacyExportAuditWriter auditWriter,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<PrivacyEndpointsOptions> options,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        ExportRequestStatus? status = await tracker.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
        ProblemHttpResult? gate = Gate(httpContext, options.Value, timeProvider, status, requestId, userId);
        if (gate is not null)
        {
            return gate;
        }

        try
        {
            PrivacyExportDownloadPayload payload = await downloadResolver
                .OpenShardAsync(requestId, shardIndex, cancellationToken).ConfigureAwait(false);
            await WriteShardDownloadAuditAsync(auditWriter, httpContext, currentTenant, requestId, userId, status!.SubjectUserId, shardIndex, timeProvider, cancellationToken).ConfigureAwait(false);
            return ToFileResult(payload);
        }
        catch (ArgumentOutOfRangeException)
        {
            return TypedResults.Problem(
                detail: $"Shard {shardIndex} is out of range for export '{requestId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (PrivacyExportNotReadyException)
        {
            return ExportNotReady(requestId);
        }
    }

    // The analyzer that enforces [FromServices] on interface params keys off the
    // method signature, not the registration path — this is a private helper, not a
    // route handler. Pass the already-resolved status in to keep the analyzer happy
    // and avoid hidden DI assumptions inside helpers.
    private static ProblemHttpResult? Gate(
        HttpContext httpContext,
        PrivacyEndpointsOptions endpointOptions,
        TimeProvider timeProvider,
        ExportRequestStatus? status,
        Guid requestId,
        Guid callerUserId)
    {
        // Download endpoints are SUBJECT-ONLY by design: the admin who triggered
        // an on-behalf-of DSR can observe completion via GET /exports/{id} (status
        // read), but only the data subject can receive the actual archive bytes —
        // the data goes to the subject's notification email, not back to the
        // operator. 404 hides the existence of subjects in other tenants.
        if (status is null || status!.SubjectUserId != callerUserId)
        {
            return TypedResults.Problem(
                detail: $"Export request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (endpointOptions.DownloadStepUpRequired
            && !PrivacyStepUpChallenge.IsAuthFresh(httpContext, endpointOptions.DownloadStepUpMaxAge, timeProvider.GetUtcNow()))
        {
            string detail = PrivacyStepUpChallenge.SetStepUpChallengeHeader(httpContext, endpointOptions.DownloadStepUpMaxAge);
            return TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static ProblemHttpResult ExportNotReady(Guid requestId) =>
        TypedResults.Problem(
            detail: $"Export request '{requestId}' has no archive available yet. Poll GET /privacy/exports/{requestId} for status.",
            statusCode: StatusCodes.Status409Conflict);

    private static FileStreamHttpResult ToFileResult(PrivacyExportDownloadPayload payload) =>
        TypedResults.Stream(payload.Content, payload.ContentType, payload.FileName);

    /// <summary>Sentinel ShardIndex value used in the audit payload for manifest downloads.</summary>
    private const int ManifestShardSentinel = -1;

    private static Task WriteShardDownloadAuditAsync(
        IPrivacyExportAuditWriter auditWriter,
        HttpContext httpContext,
        ICurrentTenant currentTenant,
        Guid requestId,
        Guid downloaderUserId,
        Guid subjectUserId,
        int shardIndex,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        auditWriter.WriteShardDownloadedAsync(
            new PrivacyExportShardDownloadedAudit(
                RequestId: requestId,
                DownloaderUserId: downloaderUserId,
                SubjectUserId: subjectUserId,
                TenantId: currentTenant.IsAvailable ? currentTenant.Id : null,
                ShardIndex: shardIndex,
                ClientIp: PrivacyResponseMapper.PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
                UserAgent: httpContext.Request.Headers.UserAgent.ToString(),
                AuthMethod: httpContext.User.Identity?.AuthenticationType,
                CorrelationId: httpContext.TraceIdentifier,
                Timestamp: timeProvider.GetUtcNow()),
            cancellationToken);
}

