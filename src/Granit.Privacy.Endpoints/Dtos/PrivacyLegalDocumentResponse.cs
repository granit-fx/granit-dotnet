namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// A legal document registered in the consent registry (GDPR Art. 7).
/// </summary>
/// <param name="DocumentId">Unique identifier (e.g., "privacy-policy", "terms-of-service").</param>
/// <param name="CurrentVersion">Current version of the document (e.g., "2.1.0").</param>
/// <param name="DisplayName">Human-readable display name (for audit reports).</param>
public sealed record PrivacyLegalDocumentResponse(string DocumentId, string CurrentVersion, string DisplayName);
