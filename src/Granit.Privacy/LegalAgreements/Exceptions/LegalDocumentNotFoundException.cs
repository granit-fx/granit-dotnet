namespace Granit.Privacy.LegalAgreements.Exceptions;

/// <summary>
/// Thrown when a legal-document operation targets a document id that has no matching
/// row (across drafts and published versions).
/// </summary>
/// <remarks>
/// The endpoint surface maps this to <c>404 Not Found</c>. A typed exception replaces the
/// former brittle <c>InvalidOperationException.Message.Contains("not found")</c> disambiguation.
/// </remarks>
public sealed class LegalDocumentNotFoundException(Guid documentId)
    : Exception($"Legal document '{documentId}' not found.")
{
    /// <summary>Identifier of the legal document that could not be found.</summary>
    public Guid DocumentId { get; } = documentId;
}
