using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Publishes a legal document draft, auto-archiving the previous published version
/// and dispatching <see cref="Events.LegalAgreementObsoleteEto"/> for re-consent flows.
/// </summary>
public interface ILegalDocumentPublicationService
{
    /// <summary>
    /// Publishes the specified legal document draft. If a published version already exists
    /// for the same <see cref="LegalDocument.DocumentId"/>, it is archived in the same
    /// transaction.
    /// </summary>
    /// <param name="documentId">The entity ID of the draft to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The published document.</returns>
    Task<LegalDocument> PublishAsync(Guid documentId, CancellationToken cancellationToken = default);
}
