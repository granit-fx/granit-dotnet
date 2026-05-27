using Granit.Events;
using Granit.Guids;
using Granit.Http.Cookies;
using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Endpoints;
using Granit.Privacy.Endpoints.Options;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Events;
using Granit.Privacy.Options;
using Granit.Privacy.OptOut;
using Granit.Privacy.OptOut.Events;
using Granit.Privacy.ProcessingPurposes;
using Granit.Privacy.Regulations;
using Granit.RateLimiting.AspNetCore;
using Granit.Users;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping privacy endpoints.
/// </summary>
public static class PrivacyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps privacy endpoints under <c>/{prefix}/privacy</c>: data export (Art. 15/20),
    /// data deletion (Art. 17), and legal agreement consent (Art. 7).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="PrivacyEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitPrivacy(
        this IEndpointRouteBuilder endpoints,
        Action<PrivacyEndpointsOptions>? configure = null)
    {
        PrivacyEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        if (!string.IsNullOrEmpty(options.RateLimitingPolicy))
        {
            group.RequireRateLimiting(options.RateLimitingPolicy);
        }

        RegisterOptOutCookie(endpoints.ServiceProvider.GetRequiredService<ICookieRegistry>());

        MapRegulationEndpoints(group);
        MapPurposeEndpoints(group);
        MapOptOutEndpoints(group);
        MapExportEndpoints(group);
        MapDeletionEndpoints(group);
        MapAgreementEndpoints(group);
        MapLegalDocumentAdminEndpoints(group);

        return group;
    }

    // -------------------------------------------------------------------------
    // Regulation
    // -------------------------------------------------------------------------

    private static void MapRegulationEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/regulation", HandleGetRegulationAsync)
             .WithName("GetApplicableRegulation")
             .WithSummary("Returns the privacy regulation profile applicable to the current tenant.")
             .WithDescription(
                 "Resolves the privacy regulation for the current tenant context via IPrivacyRegulationResolver. "
                 + "Returns the full regulation profile including consent model, response timelines, breach notification "
                 + "deadlines, age thresholds, cookie consent rules, and cross-border transfer requirements.")
             .Produces<PrivacyRegulationProfileResponse>();
    }

    private static async Task<Ok<PrivacyRegulationProfileResponse>> HandleGetRegulationAsync(
        [FromServices] IPrivacyRegulationResolver resolver,
        CancellationToken cancellationToken)
    {
        PrivacyRegulationProfile profile = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new PrivacyRegulationProfileResponse(
            profile.Regulation.Value,
            profile.DisplayName,
            profile.JurisdictionCode,
            profile.ConsentModel.ToString(),
            profile.AvailableLegalBases.Select(b => b.Value).ToList(),
            profile.SubjectAccessRequestDays,
            profile.SubjectAccessRequestExtensionDays,
            profile.DeletionRequestDays,
            profile.DefaultDeletionGracePeriodDays,
            profile.MaxDeletionGracePeriodDays,
            profile.BreachNotifyAuthorityHours,
            profile.BreachNotifyIndividualsHours,
            profile.MinimumConsentAge,
            profile.RequiresParentalIdentityVerification,
            profile.CookieConsentModel.ToString(),
            profile.HonorGlobalPrivacyControl,
            profile.RequiresCrossBorderAssessment,
            profile.TransferMechanisms.ToList(),
            profile.DataLocalizationRequired,
            profile.RequiresDpoOrRepresentative,
            profile.RequiredExportFormats.ToList()));
    }

    // -------------------------------------------------------------------------
    // Processing purposes
    // -------------------------------------------------------------------------

    private static void MapPurposeEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/purposes", HandleGetPurposesAsync)
             .RequireAuthorization(PrivacyPermissions.Purposes.Read)
             .WithName("ListProcessingPurposes")
             .WithSummary("Returns all registered processing purposes with their legal bases.")
             .WithDescription(
                 "Lists all processing purposes declared at application startup via RegisterProcessingPurpose(). "
                 + "Each purpose includes its legal basis, whether explicit consent is required, and an optional data category.")
             .Produces<IReadOnlyList<PrivacyProcessingPurposeResponse>>();
    }

    private static Task<Ok<IReadOnlyList<PrivacyProcessingPurposeResponse>>> HandleGetPurposesAsync(
        [FromServices] IProcessingPurposeRegistry purposeRegistry)
    {
        IReadOnlyList<PrivacyProcessingPurposeResponse> result = purposeRegistry.GetAll()
            .Select(p => new PrivacyProcessingPurposeResponse(
                p.PurposeId, p.DisplayName, p.Description, p.LegalBasis,
                p.RequiresExplicitConsent, p.DataCategory))
            .ToList();

        return Task.FromResult(TypedResults.Ok(result));
    }

    // -------------------------------------------------------------------------
    // Opt-out (CCPA "Do Not Sell or Share")
    // -------------------------------------------------------------------------

    private static void MapOptOutEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/opt-out", HandleOptOutAsync)
             .AllowAnonymous()
             .RequireGranitRateLimiting(PrivacyOptOutRateLimitPolicies.OptOutCreate)
             .WithName("RequestOptOut")
             .WithSummary("Opts out of data sale/sharing (CCPA).")
             .WithDescription(
                 "Records a 'Do Not Sell or Share My Personal Information' request. "
                 + "Supports both authenticated users and anonymous visitors (CCPA compliance). "
                 + "For anonymous visitors, a _optout_id HTTP-Only cookie is set to track the opt-out. "
                 + "Anonymous opt-outs are stored tenant-less by default (PrivacyEndpointsOptions."
                 + nameof(PrivacyEndpointsOptions.BindAnonymousOptOutToCurrentTenant) + ") to prevent "
                 + "tenant injection via a spoofable resolver. Rate-limited via the "
                 + "`privacy-optout-create` policy.")
             .Produces<PrivacyOptOutStatusResponse>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status429TooManyRequests)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/opt-out/status", HandleGetOptOutStatusAsync)
             .AllowAnonymous()
             .WithName("GetOptOutStatus")
             .WithSummary("Returns the current opt-out status.")
             .WithDescription(
                 "Checks whether the requesting user or visitor has an active opt-out. "
                 + "For authenticated users, checks by UserId. For anonymous visitors, checks the _optout_id cookie.")
             .Produces<PrivacyOptOutStatusResponse>();
    }

    private static async Task<Results<Created<PrivacyOptOutStatusResponse>, ProblemHttpResult>> HandleOptOutAsync(
        HttpContext httpContext,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IOptOutRecordReader? optOutReader,
        [FromServices] IOptOutRecordWriter? optOutWriter,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] TimeProvider timeProvider,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] IGranitCookieManager cookieManager,
        [FromServices] IOptions<PrivacyEndpointsOptions> endpointOptions,
        CancellationToken cancellationToken)
    {
        if (optOutWriter is null)
        {
            return TypedResults.Problem(
                detail: "Opt-out functionality requires an IOptOutRecordStore registration via UseOptOutRecordStore<T>().",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        Guid recordId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        string regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        // Determine identity — authenticated user or anonymous visitor
        Guid? userId = TryGetUserIdOrNull(httpContext);
        string? anonymousTrackId = null;

        if (userId is null)
        {
            // Anonymous: read existing cookie or generate new tracking ID
            anonymousTrackId = httpContext.Request.Cookies[OptOutConstants.CookieName]
                ?? guidGenerator.Create().ToString();

            await cookieManager.SetCookieAsync(httpContext, OptOutConstants.CookieName, anonymousTrackId)
                .ConfigureAwait(false);
        }

        // Anonymous flows must not trust ICurrentTenant by default — many hosts resolve
        // tenancy from an attacker-controllable header (X-Tenant-Id) for cross-cutting
        // routes, which would let an unauthenticated client poison an arbitrary tenant's
        // opt-out store. Authenticated calls keep their tenant context.
        bool trustsAnonymousTenant = endpointOptions.Value.BindAnonymousOptOutToCurrentTenant;
        Guid? tenantId = userId is not null || trustsAnonymousTenant
            ? ResolveTenantId(currentTenant)
            : null;

        // Idempotency: return existing opt-out if already active
        if (optOutReader is not null)
        {
            bool alreadyOptedOut = await optOutReader.IsOptedOutAsync(userId, anonymousTrackId, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyOptedOut)
            {
                return TypedResults.Created(
                    (string?)null,
                    new PrivacyOptOutStatusResponse(true, null, regulation));
            }
        }

        OptOutRecord record = new(recordId, userId, anonymousTrackId, OptOutState.Active, now, null, tenantId, regulation);
        await optOutWriter.RecordOptOutAsync(record, cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new OptOutRequestedEto(recordId, userId, anonymousTrackId, now, regulation, tenantId),
            cancellationToken).ConfigureAwait(false);

        metrics.RecordOptOutRequested(tenantId, regulation);

        return TypedResults.Created(
            (string?)null,
            new PrivacyOptOutStatusResponse(true, now, regulation));
    }

    private static async Task<Ok<PrivacyOptOutStatusResponse>> HandleGetOptOutStatusAsync(
        HttpContext httpContext,
        [FromServices] IOptOutRecordReader? optOutReader,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (optOutReader is null)
        {
            return TypedResults.Ok(new PrivacyOptOutStatusResponse(false, null, null));
        }

        Guid? userId = TryGetUserIdOrNull(httpContext);
        string? anonymousTrackId = httpContext.Request.Cookies[OptOutConstants.CookieName];

        bool isOptedOut = await optOutReader.IsOptedOutAsync(userId, anonymousTrackId, cancellationToken).ConfigureAwait(false);

        string? regulation = null;
        if (isOptedOut)
        {
            regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Ok(new PrivacyOptOutStatusResponse(isOptedOut, null, regulation));
    }

    internal static Guid? TryGetUserIdOrNull(HttpContext httpContext)
    {
        System.Security.Claims.Claim? sub = httpContext.User.FindFirst("sub")
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        return sub is not null && Guid.TryParse(sub.Value, out Guid userId) ? userId : null;
    }

    // -------------------------------------------------------------------------
    // Export (GDPR Art. 15/20)
    // -------------------------------------------------------------------------

    private static void MapExportEndpoints(RouteGroupBuilder group)
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
             .Produces<IReadOnlyList<PrivacyExportScopeResponse>>();

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
             .ProducesProblem(StatusCodes.Status404NotFound)
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
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/exports", HandleGetMyExportsAsync)
             .WithName("ListPrivacyExports")
             .WithSummary("Lists all export requests for the current user.")
             .WithDescription(
                 "Returns all export requests submitted by the current user, ordered by most recent first. "
                 + "Each entry includes the request state, timestamps, and archive reference when available.")
             .Produces<IReadOnlyList<PrivacyExportStatusResponse>>();

        group.MapPrivacyExportDownloadEndpoints();
    }

    // -------------------------------------------------------------------------
    // Deletion (GDPR Art. 17)
    // -------------------------------------------------------------------------

    private static void MapDeletionEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/deletions", HandleRequestDeletionAsync)
             .RequireAuthorization(PrivacyPermissions.Deletions.Execute)
             .WithName("RequestPrivacyDeletion")
             .WithSummary("Requests personal data deletion for the current user.")
             .WithDescription(
                 "Publishes a distributed deletion event (GDPR Art. 17 — right to erasure). "
                 + "When Defer is false, deletion is immediate. When Defer is true, a cooling-off "
                 + "period starts — the user receives a reminder email before the "
                 + "deadline and can cancel via POST /deletion/{requestId}/cancel. "
                 + "A confirmation email is sent in both cases after deletion is executed. "
                 + "Do not include personally identifiable information in the Reason field.")
             .Produces<PrivacyDeletionRequestResponse>(StatusCodes.Status202Accepted)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesValidationProblem();

        group.MapPost("/deletions/{requestId:guid}/cancel", HandleCancelDeletionAsync)
             .RequireAuthorization(PrivacyPermissions.Deletions.Execute)
             .WithName("CancelPrivacyDeletion")
             .WithSummary("Cancels a deferred deletion request during the grace period.")
             .WithDescription(
                 "Cancels a deferred deletion request. Only requests in Deferred state can be "
                 + "cancelled. Returns 404 if the request is not found, 409 if already executed or cancelled.")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/deletions/{requestId:guid}", HandleGetDeletionStatusAsync)
             .WithName("GetPrivacyDeletionStatus")
             .WithSummary("Returns the status of a deferred deletion request.")
             .WithDescription(
                 "Queries the deletion request tracker for the specified request ID. "
                 + "Only the user who created the request can view its status. "
                 + "Returns the current state (Deferred, Executed, Cancelled), scheduled deletion date, "
                 + "and timestamps. Returns 404 if the request is not found or belongs to another user.")
             .Produces<PrivacyDeletionStatusResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/deletions", HandleGetMyDeletionsAsync)
             .WithName("ListPrivacyDeletions")
             .WithSummary("Lists all deletion requests for the current user.")
             .WithDescription(
                 "Returns all deferred deletion requests submitted by the current user, "
                 + "ordered by most recent first. Immediate deletions are not tracked.")
             .Produces<IReadOnlyList<PrivacyDeletionStatusResponse>>();
    }

    // -------------------------------------------------------------------------
    // Legal Agreements (GDPR Art. 7)
    // -------------------------------------------------------------------------

    private static void MapAgreementEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/agreements/documents", HandleGetDocumentsAsync)
             .RequireAuthorization(PrivacyPermissions.Agreements.Read)
             .WithName("ListPrivacyLegalDocuments")
             .WithSummary("Returns all registered legal documents.")
             .WithDescription(
                 "Returns all legal documents registered in the consent registry (GDPR Art. 7). "
                 + "Each document includes its identifier, current version, and display name. "
                 + "Use GET /agreements/status to check the user's consent per document.")
             .Produces<IReadOnlyList<PrivacyLegalDocumentResponse>>();

        group.MapGet("/agreements/status", HandleGetConsentStatusAsync)
             .RequireAuthorization(PrivacyPermissions.Agreements.Read)
             .WithName("GetPrivacyConsentStatus")
             .WithSummary("Returns the consent status for all legal documents.")
             .WithDescription(
                 "For each registered legal document, returns whether the current user has accepted "
                 + "the latest version and when the last acceptance occurred. "
                 + "Documents where HasAcceptedLatest is false require re-consent.")
             .Produces<IReadOnlyList<PrivacyConsentStatusResponse>>();

        group.MapGet("/agreements/history", HandleGetAgreementHistoryAsync)
             .RequireAuthorization(PrivacyPermissions.Agreements.Read)
             .WithName("ListPrivacyAgreementHistory")
             .WithSummary("Returns the consent history for the current user.")
             .WithDescription(
                 "Returns all consent records for the current user, ordered by most recent first. "
                 + "Each record includes the document ID, accepted version, timestamp, and whether "
                 + "it matches the current document version.")
             .Produces<IReadOnlyList<PrivacyUserAgreementResponse>>();

        group.MapPost("/agreements/accept", HandleAcceptAgreementAsync)
             .RequireAuthorization(PrivacyPermissions.Agreements.Create)
             .WithName("AcceptPrivacyAgreement")
             .WithSummary("Records acceptance of a legal agreement.")
             .WithDescription(
                 "Records the user's consent for a specific legal document version (GDPR Art. 7). "
                 + "The version must match the current version — stale consent is rejected (422). "
                 + "If the user has already accepted the latest version, returns 409. "
                 + "The client IP address is captured and pseudonymized for the audit trail. "
                 + "Ensure ForwardedHeadersMiddleware is enabled when running behind a reverse proxy.")
             .Produces(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesValidationProblem();
    }

    // -------------------------------------------------------------------------
    // Export handlers
    // -------------------------------------------------------------------------

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
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

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
                ClientIp: PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
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

        if (!TryGetUserId(currentUser, out Guid callerUserId))
        {
            return UserNotAuthenticated();
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
        string regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

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
                ClientIp: PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
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
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        Guid? tenantGuid = currentTenant.IsAvailable ? currentTenant.Id : null;
        string regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

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
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
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

        return TypedResults.Ok(MapExportStatus(status));
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyExportStatusResponse>>, ProblemHttpResult>> HandleGetMyExportsAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IExportRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        IReadOnlyList<ExportRequestStatus> statuses = await tracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyExportStatusResponse> result = statuses
            .Select(MapExportStatus)
            .ToList();

        return TypedResults.Ok(result);
    }

    // -------------------------------------------------------------------------
    // Deletion handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Accepted<PrivacyDeletionRequestResponse>, Accepted, ProblemHttpResult>> HandleRequestDeletionAsync(
        PrivacyDeletionRequest body,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IDeletionRequestTrackerReader deletionTracker,
        [FromServices] IDeletionRequestTrackerWriter? deletionTrackerWriter,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] TimeProvider timeProvider,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<GranitPrivacyOptions> privacyOptions,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        IReadOnlyList<DeletionRequestStatus> existing = await deletionTracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (existing.Any(r => r.State == DeletionRequestState.Deferred))
        {
            return TypedResults.Problem(
                detail: "A deferred deletion request is already in progress. Cancel it before submitting a new one.",
                statusCode: StatusCodes.Status409Conflict);
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = ResolveTenantId(currentTenant);
        string requestedBy = currentUser.Email ?? "unknown";
        string regulation = await ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        if (body.Defer)
        {
            int graceDays = privacyOptions.Value.DefaultGracePeriodDays;
            DateTimeOffset scheduledDeletionAt = now.AddDays(graceDays);

            await eventBus
                .PublishAsync(
                    new DeletionDeferredEto(requestId, userId, requestedBy, now, body.Reason, scheduledDeletionAt, regulation, tenantId),
                    cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordDeletionDeferred(tenantId, regulation);

            return TypedResults.Accepted(
                $"/privacy/deletion/{requestId}",
                new PrivacyDeletionRequestResponse(requestId, scheduledDeletionAt));
        }

        // Immediate deletion + confirmation event + audit trail (GDPR Art. 5(2))
        if (deletionTrackerWriter is not null)
        {
            await deletionTrackerWriter
                .RecordImmediateDeletionAsync(requestId, userId, body.Reason, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await eventBus
            .PublishAsync(
                new PersonalDataDeletionRequestedEto(requestId, userId, requestedBy, now, body.Reason, regulation, tenantId),
                cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new DeletionExecutedEto(requestId, userId, now),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordDeletionRequested(tenantId, regulation);
        metrics.RecordDeletionExecuted(tenantId, regulation);

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Ok, ProblemHttpResult>> HandleCancelDeletionAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        DeletionRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (status.UserId != userId)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (status.State != DeletionRequestState.Deferred)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' is already {status.State} and cannot be cancelled.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        await eventBus
            .PublishAsync(
                new DeletionCancelledEto(requestId, userId, now),
                cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<PrivacyDeletionStatusResponse>, ProblemHttpResult>> HandleGetDeletionStatusAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        DeletionRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null || status.UserId != userId)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(MapDeletionStatus(status));
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyDeletionStatusResponse>>, ProblemHttpResult>> HandleGetMyDeletionsAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        IReadOnlyList<DeletionRequestStatus> statuses = await tracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyDeletionStatusResponse> result = statuses
            .Select(MapDeletionStatus)
            .ToList();

        return TypedResults.Ok(result);
    }

    // -------------------------------------------------------------------------
    // Agreement handlers
    // -------------------------------------------------------------------------

    private static Ok<IReadOnlyList<PrivacyLegalDocumentResponse>> HandleGetDocumentsAsync(
        [FromServices] ILegalDocumentRegistry registry)
    {
        IReadOnlyList<PrivacyLegalDocumentResponse> result = registry
            .GetAll()
            .Select(d => new PrivacyLegalDocumentResponse(d.DocumentId, d.CurrentVersion, d.DisplayName))
            .ToList();

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyConsentStatusResponse>>, ProblemHttpResult>> HandleGetConsentStatusAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] ILegalDocumentRegistry registry,
        [FromServices] ILegalAgreementChecker checker,
        [FromServices] ILegalAgreementStoreReader storeReader,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        IReadOnlyList<LegalDocumentDefinition> documents = registry.GetAll();
        List<PrivacyConsentStatusResponse> result = new(documents.Count);

        foreach (LegalDocumentDefinition doc in documents)
        {
            bool hasAccepted = await checker
                .HasAcceptedLatestAsync(userId, doc.DocumentId, cancellationToken)
                .ConfigureAwait(false);

            LegalAgreements.Domain.LegalAgreementBase? latest = await storeReader
                .FindLatestAsync(userId, doc.DocumentId, cancellationToken)
                .ConfigureAwait(false);

            result.Add(new PrivacyConsentStatusResponse(
                doc.DocumentId,
                doc.CurrentVersion,
                hasAccepted,
                latest?.AcceptedAt));
        }

        return TypedResults.Ok<IReadOnlyList<PrivacyConsentStatusResponse>>(result);
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyUserAgreementResponse>>, ProblemHttpResult>> HandleGetAgreementHistoryAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] ILegalAgreementChecker checker,
        [FromServices] ILegalDocumentRegistry registry,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        IReadOnlyList<LegalAgreements.Domain.LegalAgreementBase> agreements = await checker
            .GetUserAgreementsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyUserAgreementResponse> result = agreements
            .Select(a => new PrivacyUserAgreementResponse(
                a.Id,
                a.DocumentId,
                a.Version,
                a.AcceptedAt,
                registry.GetDefinition(a.DocumentId)?.CurrentVersion == a.Version))
            .ToList();

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created, ProblemHttpResult>> HandleAcceptAgreementAsync(
        PrivacyAcceptAgreementRequest body,
        HttpContext httpContext,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] ILegalDocumentRegistry registry,
        [FromServices] ILegalAgreementChecker checker,
        [FromServices] ILegalAgreementStoreWriter storeWriter,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(currentUser, out Guid userId))
        {
            return UserNotAuthenticated();
        }

        LegalDocumentDefinition? document = registry.GetDefinition(body.DocumentId);
        if (document is null)
        {
            return TypedResults.Problem(
                detail: $"Legal document '{body.DocumentId}' is not registered.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!string.Equals(body.Version, document.CurrentVersion, StringComparison.Ordinal))
        {
            return TypedResults.Problem(
                detail: $"Version mismatch: expected '{document.CurrentVersion}', got '{body.Version}'. The user must accept the latest version.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        bool alreadyAccepted = await checker
            .HasAcceptedLatestAsync(userId, body.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyAccepted)
        {
            return TypedResults.Problem(
                detail: $"User has already accepted version '{body.Version}' of document '{body.DocumentId}'.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string? ipAddress = PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString());

        await storeWriter
            .RecordConsentAsync(userId, body.DocumentId, body.Version, ipAddress, now, cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new LegalAgreementAcceptedEto(userId, body.DocumentId, body.Version, now),
                cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created((string?)null);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static bool TryGetUserId(ICurrentUserService currentUser, out Guid userId)
    {
        userId = Guid.Empty;

        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return false;
        }

        return Guid.TryParse(currentUser.UserId, out userId);
    }

    internal static ProblemHttpResult UserNotAuthenticated() =>
        TypedResults.Problem(
            detail: "User is not authenticated or has no valid user ID.",
            statusCode: StatusCodes.Status401Unauthorized);

    internal static Guid? ResolveTenantId(ICurrentTenant currentTenant) =>
        currentTenant.IsAvailable ? currentTenant.Id : null;

    internal static void RegisterOptOutCookie(ICookieRegistry registry) =>
        registry.Register(new CookieDefinition(
            OptOutConstants.CookieName,
            CookieCategory.StrictlyNecessary,
            OptOutConstants.RetentionDays,
            true,
            "CCPA anonymous opt-out tracking identifier"));

    internal static async Task<string> ResolveRegulationAsync(
        IPrivacyRegulationResolver? resolver,
        CancellationToken cancellationToken)
    {
        if (resolver is null)
        {
            return "EU_GDPR";
        }

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        return profile.Regulation.Value;
    }

    internal static PrivacyExportStatusResponse MapExportStatus(ExportRequestStatus status) =>
        new(
            status.RequestId,
            status.State.ToString(),
            status.RequestedAt,
            status.CompletedAt,
            status.ArchiveBlobReferenceId,
            status.MissingProviders);

    internal static PrivacyDeletionStatusResponse MapDeletionStatus(DeletionRequestStatus status) =>
        new(
            status.RequestId,
            status.State.ToString(),
            status.Reason,
            status.RequestedAt,
            status.ScheduledDeletionAt,
            status.CancelledAt,
            status.ExecutedAt);

    /// <summary>
    /// Maps the legal document administration endpoints (create, read, update, publish).
    /// </summary>
    private static void MapLegalDocumentAdminEndpoints(RouteGroupBuilder group)
    {
        group.MapGranitGroup("/legal-documents")
            .MapLegalDocumentAdminEndpoints();
    }

    internal static string? PseudonymizeIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return null;
        }

        if (!System.Net.IPAddress.TryParse(ipAddress, out System.Net.IPAddress? ip))
        {
            return null;
        }

        byte[] bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4: mask to /16 — zero last 2 octets
            bytes[2] = 0;
            bytes[3] = 0;
            return new System.Net.IPAddress(bytes).ToString();
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6: mask to /48 — zero last 10 bytes (80 bits)
            for (int i = 6; i < 16; i++)
            {
                bytes[i] = 0;
            }

            return new System.Net.IPAddress(bytes).ToString();
        }

        return null;
    }
}
