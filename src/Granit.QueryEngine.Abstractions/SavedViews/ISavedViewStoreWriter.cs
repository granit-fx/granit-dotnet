using Granit.QueryEngine.SavedViews.Domain;

namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Write-side persistence abstraction for saved views.
/// Default implementation is a null-object that throws; use
/// <c>Granit.QueryEngine.EntityFrameworkCore</c> for a concrete store.
/// </summary>
public interface ISavedViewStoreWriter
{
    /// <summary>
    /// Creates a new saved view.
    /// </summary>
    /// <param name="view">The saved view to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(SavedView view, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing saved view.
    /// </summary>
    /// <param name="view">The saved view to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(SavedView view, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved view by identifier.
    /// </summary>
    /// <param name="id">The saved view identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a saved view as the default for a user and entity type.
    /// Unsets any previous default for the same user and entity type.
    /// </summary>
    /// <param name="id">The saved view identifier to set as default.</param>
    /// <param name="userId">The current user identifier.</param>
    /// <param name="entityType">The query definition name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetDefaultAsync(Guid id, string userId, string entityType, CancellationToken cancellationToken = default);
}
