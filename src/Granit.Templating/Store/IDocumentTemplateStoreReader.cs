using Granit.Templating.Keys;
using Granit.Templating.Pipeline;

namespace Granit.Templating.Store;

/// <summary>
/// Read-side contract for accessing published templates and revision history.
/// </summary>
/// <remarks>
/// Implemented by <c>EfDocumentTemplateStore</c> in <c>Granit.Templating.EntityFrameworkCore</c>.
/// A <c>CachedDocumentTemplateStore</c> decorator wraps it with a hybrid memory/Redis cache.
/// </remarks>
public interface IDocumentTemplateStoreReader
{
    /// <summary>
    /// Returns the currently published template for the given key, or <c>null</c> if none exists.
    /// </summary>
    /// <param name="key">Template key (name + optional culture).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TemplateDescriptor?> TryGetPublishedAsync(
        TemplateKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current draft revision for the given key, or <c>null</c> if no draft exists.
    /// </summary>
    /// <param name="key">Template key (name + optional culture).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TemplateRevision?> TryGetDraftAsync(
        TemplateKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full revision history for the given key, ordered by creation date (newest first).
    /// </summary>
    /// <param name="key">Template key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<TemplateRevision>> GetHistoryAsync(
        TemplateKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all distinct non-null layout names assigned to templates in the store.
    /// Used by admin endpoints to populate layout dropdowns.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetDistinctLayoutNamesAsync(
        CancellationToken cancellationToken = default);
}
