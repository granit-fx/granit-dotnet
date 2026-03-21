namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Request to initiate personal data deletion (GDPR Art. 17 — right to erasure).
/// </summary>
/// <param name="Reason">Reason for the deletion request (e.g., "Account closure", "Withdrawal of consent").</param>
public sealed record PrivacyDeletionRequest(string Reason);
