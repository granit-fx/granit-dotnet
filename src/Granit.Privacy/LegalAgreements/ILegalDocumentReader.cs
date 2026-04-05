using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Reads <see cref="LegalDocument"/> entities (query side of CQRS).
/// </summary>
public interface ILegalDocumentReader
{
    /// <summary>Returns the currently published version for the given document ID, or <c>null</c>.</summary>
    Task<LegalDocument?> FindPublishedAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>Returns all currently published legal documents.</summary>
    Task<IReadOnlyList<LegalDocument>> GetAllPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a legal document by its entity ID (including drafts and archived versions).</summary>
    Task<LegalDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all versions of a document, ordered by version descending.</summary>
    Task<IReadOnlyList<LegalDocument>> GetVersionHistoryAsync(string documentId, CancellationToken cancellationToken = default);
}
