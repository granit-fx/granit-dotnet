using Granit.Core.Events;

namespace Granit.Privacy.LegalAgreements.Events;

/// <summary>
/// Published when a legal document version is superseded by a new version.
/// Triggers re-consent flows for affected users.
/// </summary>
/// <param name="DocumentId">The legal document identifier.</param>
/// <param name="OldVersion">The superseded version.</param>
/// <param name="NewVersion">The new active version.</param>
public sealed record LegalAgreementObsoleteEto(
    string DocumentId,
    string OldVersion,
    string NewVersion) : IIntegrationEvent;
