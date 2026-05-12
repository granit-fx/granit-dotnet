using Granit.BlobStorage;
using Granit.Documents.PublicLinks.Diagnostics;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.Endpoints.Dtos;
using Granit.Documents.PublicLinks.Endpoints.Internal;
using Granit.Documents.PublicLinks.Endpoints.Options;
using Granit.Documents.PublicLinks.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Granit.Documents.PublicLinks.Endpoints.Endpoints;

/// <summary>
/// HTTP endpoints for the public-links module (F18.3).
/// <para>
/// Two surfaces are mounted side by side:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Admin</b> (authenticated, permission-gated): mint, revoke, and list public
///     links attached to a document.
///   </item>
///   <item>
///     <b>Anonymous</b> (no auth required): exchange a bearer token for a short-lived
///     presigned blob URL. Every failure path returns <c>404 Not Found</c> with no body
///     to prevent information disclosure (no distinction between unknown / revoked /
///     expired / exhausted / mis-typed).
///   </item>
/// </list>
/// </summary>
internal static partial class PublicLinksEndpoints
{
    /// <summary>Maps the admin (authenticated) endpoints onto the supplied group.</summary>
    public static RouteGroupBuilder MapAdminEndpoints(
        this RouteGroupBuilder group,
        DocumentsPublicLinksEndpointsOptions options)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(options);

        group.MapPost("/documents/{id:guid}/public-links", CreateAsync)
            .WithName("CreateDocumentPublicLink")
            .WithSummary("Mints a fresh public link for a document.")
            .WithDescription(
                "Generates a 32-byte random bearer token, persists only its HMAC-SHA256 "
                + "digest, and returns the raw token together with the fully-qualified "
                + "redemption URL. The token is shown exactly once and is never recoverable "
                + "after this response. `ttlDays` is clamped down to the configured maximum; "
                + "`maxUses` falls back to the configured default when omitted. Returns 422 "
                + "when the document is missing or excluded by the tenant filter.")
            .RequireAuthorization(DocumentsPublicLinksPermissions.PublicLinks.Create)
            .Produces<CreatePublicLinkResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem();

        group.MapDelete("/public-links/{id:guid}", RevokeAsync)
            .WithName("RevokeDocumentPublicLink")
            .WithSummary("Revokes an existing public link.")
            .WithDescription(
                "Idempotency is not enforced: revoking an already-revoked link returns 422. "
                + "The optional `reason` is persisted on the aggregate and surfaced on the "
                + "audit trail (DocumentPublicLinkRevokedEvent / Eto).")
            .RequireAuthorization(DocumentsPublicLinksPermissions.PublicLinks.Revoke)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/documents/{id:guid}/public-links", ListAsync)
            .WithName("ListDocumentPublicLinks")
            .WithSummary("Lists every public link issued against a document.")
            .WithDescription(
                "Newest-first listing. The raw token and its HMAC digest are deliberately "
                + "omitted — the bearer is only ever shown at creation time. Returns an "
                + "empty array when the document has no links (or does not exist for the "
                + "current tenant).")
            .RequireAuthorization(DocumentsPublicLinksPermissions.PublicLinks.Read)
            .Produces<IReadOnlyList<PublicLinkResponse>>();

        return group;
    }

    /// <summary>Maps the anonymous redemption endpoints onto the supplied group.</summary>
    public static RouteGroupBuilder MapAnonymousEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{token}", DownloadAsync)
            .WithName("RedeemDocumentPublicLinkDownload")
            .WithSummary("Resolves a bearer token and serves the document as an attachment.")
            .WithDescription(
                "Anonymous endpoint — no authentication is required. The token is hashed "
                + "with the host's signing key, looked up bypassing the tenant filter, and "
                + "validated (not revoked, not expired, MaxUses not exhausted). On success "
                + "the response is a 302 redirect to a short-lived presigned blob URL with "
                + "`Content-Disposition: attachment`. EVERY failure path returns 404 with no "
                + "body — invalid, expired, revoked, exhausted and not-yet-finalised links "
                + "are indistinguishable on the wire.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{token}/preview", PreviewAsync)
            .WithName("RedeemDocumentPublicLinkPreview")
            .WithSummary("Resolves a bearer token and serves the document inline.")
            .WithDescription(
                "Anonymous endpoint — no authentication is required. Same resolution path "
                + "as the download endpoint, but the presigned URL is issued without a "
                + "forced `Content-Disposition: attachment` header so the browser may "
                + "preview the bytes inline. EVERY failure path returns 404 with no body.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers — admin
    // -------------------------------------------------------------------------

    private static async Task<Results<Created<CreatePublicLinkResponse>, ProblemHttpResult>> CreateAsync(
        Guid id,
        CreatePublicLinkRequest request,
        [FromServices] IDocumentPublicLinkService service,
        [FromServices] DocumentsPublicLinksMetrics metrics,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(typeof(PublicLinksEndpoints).FullName!);
        try
        {
            DocumentPublicLinkCreationResult result = await service
                .CreateAsync(id, request.Scope, TimeSpan.FromDays(request.TtlDays), request.MaxUses, cancellationToken)
                .ConfigureAwait(false);

            DocumentPublicLink link = result.Link;
            metrics.RecordCreated(link.TenantId?.ToString(), link.Scope.ToString());
            LogCreated(logger, link.Id, link.DocumentId, link.Scope);

            string redemptionPath = link.Scope == PublicLinkScope.View ? "preview" : string.Empty;
            string url = BuildRedemptionUrl(httpContext, result.Token.Value, redemptionPath);

            var response = new CreatePublicLinkResponse(
                Id: link.Id,
                DocumentId: link.DocumentId,
                Token: result.Token.Value,
                Url: url,
                Scope: link.Scope,
                ExpiresAt: link.ExpiresAt,
                MaxUses: link.MaxUses);
            return TypedResults.Created($"/public-links/{link.Id}", response);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeAsync(
        Guid id,
        [FromServices] IDocumentPublicLinkService service,
        [FromServices] DocumentsPublicLinksMetrics metrics,
        [FromServices] ILoggerFactory loggerFactory,
        RevokePublicLinkRequest? request,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(typeof(PublicLinksEndpoints).FullName!);
        try
        {
            await service.RevokeAsync(id, request?.Reason, cancellationToken).ConfigureAwait(false);
            metrics.RecordRevoked(tenantId: null, request?.Reason ?? "operator");
            LogRevoked(logger, id);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Ok<IReadOnlyList<PublicLinkResponse>>> ListAsync(
        Guid id,
        [FromServices] IDocumentPublicLinkService service,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentPublicLink> links = await service
            .ListForDocumentAsync(id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PublicLinkResponse> response = [.. links.Select(PublicLinkMappingExtensions.ToResponse)];
        return TypedResults.Ok(response);
    }

    // -------------------------------------------------------------------------
    // Handlers — anonymous redemption
    // -------------------------------------------------------------------------

    private static async Task<Results<RedirectHttpResult, NotFound>> DownloadAsync(
        string token,
        [FromServices] IDocumentPublicLinkService service,
        [FromServices] DocumentsPublicLinksMetrics metrics,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(typeof(PublicLinksEndpoints).FullName!);
        return await RedeemAsync(token, forceAttachment: true, service, metrics, logger, httpContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Results<RedirectHttpResult, NotFound>> PreviewAsync(
        string token,
        [FromServices] IDocumentPublicLinkService service,
        [FromServices] DocumentsPublicLinksMetrics metrics,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(typeof(PublicLinksEndpoints).FullName!);
        return await RedeemAsync(token, forceAttachment: false, service, metrics, logger, httpContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Results<RedirectHttpResult, NotFound>> RedeemAsync(
        string token,
        bool forceAttachment,
        [FromServices] IDocumentPublicLinkService service,
        [FromServices] DocumentsPublicLinksMetrics metrics,
        [FromServices] ILogger logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // F18.4 — capture masked client IP + UA for the distributed audit event.
        // The raw IP NEVER leaves this method; the anonymiser collapses it to /24
        // (v4) or /48 (v6) before it crosses any module boundary.
        string? clientIpMasked = IpAddressAnonymizer.Mask(httpContext.Connection.RemoteIpAddress);
        string? userAgent = httpContext.Request.Headers.UserAgent is StringValues ua && ua.Count > 0
            ? ua.ToString()
            : null;

        DocumentPublicLink? link = await service
            .ResolveAndConsumeAsync(token, clientIpMasked, userAgent, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
        {
            LogRedemptionRejected(logger);
            return TypedResults.NotFound();
        }

        PresignedDownloadUrl? url = await service
            .CreateRedemptionUrlAsync(link, forceAttachment, cancellationToken)
            .ConfigureAwait(false);
        if (url is null)
        {
            LogRedemptionDocumentMissing(logger, link.Id, link.DocumentId);
            return TypedResults.NotFound();
        }

        metrics.RecordConsumed(link.TenantId?.ToString(), link.Scope.ToString());
        LogRedemptionSucceeded(logger, link.Id, link.DocumentId, link.Scope);
        return TypedResults.Redirect(url.Url.ToString());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string BuildRedemptionUrl(HttpContext httpContext, string token, string trailingSegment)
    {
        // Anonymous-prefix `p` is mounted at the host root. The reverse proxy / API
        // gateway is responsible for surfacing the correct scheme + host pair on the
        // inbound request — we trust HttpRequest.Scheme/Host here.
        HttpRequest request = httpContext.Request;
        string trailing = string.IsNullOrEmpty(trailingSegment) ? string.Empty : "/" + trailingSegment;
        return $"{request.Scheme}://{request.Host}/p/{token}{trailing}";
    }

    // -------------------------------------------------------------------------
    // LoggerMessage — the raw token and its hash are NEVER logged.
    // -------------------------------------------------------------------------

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "DocumentPublicLink {LinkId} created for document {DocumentId} with scope {Scope}.")]
    private static partial void LogCreated(ILogger logger, Guid linkId, Guid documentId, PublicLinkScope scope);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "DocumentPublicLink {LinkId} revoked.")]
    private static partial void LogRevoked(ILogger logger, Guid linkId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Public-link redemption attempt rejected (token not resolved or link no longer active).")]
    private static partial void LogRedemptionRejected(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "DocumentPublicLink {LinkId} pointed at document {DocumentId} which is no longer servable.")]
    private static partial void LogRedemptionDocumentMissing(ILogger logger, Guid linkId, Guid documentId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "DocumentPublicLink {LinkId} redeemed against document {DocumentId} with scope {Scope}.")]
    private static partial void LogRedemptionSucceeded(ILogger logger, Guid linkId, Guid documentId, PublicLinkScope scope);
}
