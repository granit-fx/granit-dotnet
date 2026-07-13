using System.Collections.Concurrent;

namespace Granit.Privacy.LegalAgreements.Internal;

/// <summary>
/// Thread-safe singleton registry of legal documents.
/// </summary>
internal sealed class LegalDocumentRegistry : ILegalDocumentRegistry
{
    private readonly ConcurrentDictionary<string, LegalDocumentDefinition> _documents =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Register(LegalDocumentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_documents.TryAdd(definition.DocumentId, definition))
        {
            throw new InvalidOperationException($"Legal document '{definition.DocumentId}' is already registered.");
        }
    }

    /// <inheritdoc/>
    public Task<LegalDocumentDefinition?> GetDefinitionAsync(
        string documentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.GetValueOrDefault(documentId));

    /// <inheritdoc/>
    public Task<IReadOnlyList<LegalDocumentDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LegalDocumentDefinition>>(_documents.Values.ToList());
}
