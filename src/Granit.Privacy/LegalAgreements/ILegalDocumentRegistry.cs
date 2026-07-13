namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Registry of legal documents declared at startup.
/// </summary>
/// <remarks>
/// Async-first: the composite implementation lazily hydrates its cache from the database on
/// first access, so a synchronous contract would force a thread-blocking
/// <c>GetAwaiter().GetResult()</c> on that cold path.
/// </remarks>
public interface ILegalDocumentRegistry
{
    /// <summary>Returns the definition for the given document ID, or <c>null</c> if not registered.</summary>
    Task<LegalDocumentDefinition?> GetDefinitionAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>Returns all registered document definitions.</summary>
    Task<IReadOnlyList<LegalDocumentDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
}
