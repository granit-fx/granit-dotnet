namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Request to accept a legal agreement (GDPR Art. 7 — proof of consent).
/// </summary>
/// <param name="DocumentId">Identifier of the legal document (e.g., "privacy-policy").</param>
/// <param name="Version">Version of the document being accepted (e.g., "2.1.0"). Must match the current version.</param>
public sealed record PrivacyAcceptAgreementRequest(string DocumentId, string Version);
