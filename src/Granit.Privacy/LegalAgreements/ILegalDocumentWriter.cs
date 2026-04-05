using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Writes <see cref="LegalDocument"/> entities (command side of CQRS).
/// </summary>
public interface ILegalDocumentWriter
{
    /// <summary>Inserts a new legal document (typically a draft).</summary>
    Task InsertAsync(LegalDocument document, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing legal document.</summary>
    Task UpdateAsync(LegalDocument document, CancellationToken cancellationToken = default);
}
