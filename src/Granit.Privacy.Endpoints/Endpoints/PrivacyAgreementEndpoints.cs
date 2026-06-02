using Granit.Events;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.LegalAgreements.Events;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyAgreementEndpoints
{
    internal static RouteGroupBuilder MapPrivacyAgreementEndpoints(this RouteGroupBuilder group)
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

        return group;
    }

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
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
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
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
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
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
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
        string? ipAddress = PrivacyResponseMapper.PseudonymizeIpAddress(httpContext.Connection.RemoteIpAddress?.ToString());

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
}
