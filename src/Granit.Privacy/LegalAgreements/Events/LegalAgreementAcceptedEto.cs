using Granit.Core.Events;

namespace Granit.Privacy.LegalAgreements.Events;

/// <summary>
/// Published when a user accepts a legal agreement (RGPD Art. 7 consent proof).
/// Enables cross-service consent tracking and audit trail.
/// </summary>
/// <param name="UserId">The user who consented.</param>
/// <param name="DocumentId">The legal document identifier (e.g., <c>"privacy-policy"</c>).</param>
/// <param name="Version">Document version that was accepted.</param>
/// <param name="AcceptedAt">Timestamp of consent.</param>
public sealed record LegalAgreementAcceptedEto(
    Guid UserId,
    string DocumentId,
    string Version,
    DateTimeOffset AcceptedAt) : IIntegrationEvent;
