namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Consent status for a single legal document (GDPR Art. 7).
/// </summary>
/// <param name="DocumentId">Legal document identifier.</param>
/// <param name="CurrentVersion">Current version of the document.</param>
/// <param name="HasAcceptedLatest">Whether the user has accepted the latest version.</param>
/// <param name="LastAcceptedAt">Timestamp of the most recent acceptance, or <c>null</c> if never accepted.</param>
public sealed record PrivacyConsentStatusResponse(
    string DocumentId,
    string CurrentVersion,
    bool HasAcceptedLatest,
    DateTimeOffset? LastAcceptedAt);
