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

    /// <summary>Updates an existing legal document with optimistic concurrency check.</summary>
    /// <param name="document">The modified document entity.</param>
    /// <param name="concurrencyStamp">Client-supplied stamp from the last read; must match the stored value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(LegalDocument document, string concurrencyStamp, CancellationToken cancellationToken = default);
}
