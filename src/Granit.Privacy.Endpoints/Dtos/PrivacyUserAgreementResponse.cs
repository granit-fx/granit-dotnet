namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// A single consent record from the user's agreement history (GDPR Art. 7).
/// </summary>
/// <param name="Id">Unique identifier of the consent record.</param>
/// <param name="DocumentId">Legal document identifier.</param>
/// <param name="Version">Version of the document that was accepted.</param>
/// <param name="AcceptedAt">Timestamp of acceptance (UTC).</param>
/// <param name="IsLatest">Whether this acceptance matches the current document version.</param>
public sealed record PrivacyUserAgreementResponse(
    Guid Id,
    string DocumentId,
    string Version,
    DateTimeOffset AcceptedAt,
    bool IsLatest);
