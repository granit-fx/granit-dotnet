using System.Collections.Concurrent;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.LegalAgreements.Internal;

/// <summary>
/// DB-first, static-fallback registry with in-memory caching.
/// Reads published <see cref="LegalDocument"/> entities from the database
/// and falls back to static <see cref="LegalDocumentRegistry"/> definitions.
/// </summary>
/// <remarks>
/// The cache is populated on first access and refreshed when
/// <see cref="Events.LegalDocumentCacheInvalidatedEto"/> is received by a Wolverine handler
/// calling <see cref="RefreshAsync"/>. This ensures all pods stay coherent in
/// multi-instance deployments.
/// </remarks>
internal sealed class CompositeLegalDocumentRegistry(
    IServiceScopeFactory scopeFactory,
    LegalDocumentRegistry staticRegistry) : ILegalDocumentRegistry
{
    private readonly ConcurrentDictionary<string, LegalDocumentDefinition> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _initialized;

    /// <inheritdoc/>
    public LegalDocumentDefinition? GetDefinition(string documentId)
    {
        EnsureInitialized();
        return _cache.GetValueOrDefault(documentId)
            ?? staticRegistry.GetDefinition(documentId);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LegalDocumentDefinition> GetAll()
    {
        EnsureInitialized();

        // Merge: DB definitions take precedence over static ones.
        Dictionary<string, LegalDocumentDefinition> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (LegalDocumentDefinition def in staticRegistry.GetAll())
        {
            merged[def.DocumentId] = def;
        }

        foreach (KeyValuePair<string, LegalDocumentDefinition> kvp in _cache)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged.Values.ToList();
    }

    /// <summary>
    /// Refreshes the in-memory cache from the database. Called by the
    /// <see cref="Events.LegalDocumentCacheInvalidatedEto"/> handler.
    /// </summary>
    internal async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ILegalDocumentReader reader = scope.ServiceProvider.GetRequiredService<ILegalDocumentReader>();

        IReadOnlyList<LegalDocument> published = await reader
            .GetAllPublishedAsync(cancellationToken).ConfigureAwait(false);

        _cache.Clear();
        foreach (LegalDocument doc in published)
        {
            _cache[doc.DocumentId] = new LegalDocumentDefinition(
                doc.DocumentId,
                doc.Version.ToString(),
                doc.DisplayName);
        }

        _initialized = true;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        // Synchronous initialization on first access — blocks once per pod lifetime.
        // Subsequent refreshes are async via the Wolverine handler.
        RefreshAsync().GetAwaiter().GetResult();
    }
}
