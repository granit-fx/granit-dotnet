using Granit.QueryEngine.SavedViews.Domain;

namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Read-side persistence abstraction for saved views.
/// Default implementation is a null-object that throws; use
/// <c>Granit.QueryEngine.EntityFrameworkCore</c> for a concrete store.
/// </summary>
public interface ISavedViewStoreReader
{
    /// <summary>
    /// Gets all saved views for an entity type visible to the user.
    /// Includes personal views and shared views.
    /// </summary>
    /// <param name="entityType">The query definition name.</param>
    /// <param name="userId">The current user identifier.</param>
    /// <param name="tenantId">The tenant identifier, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SavedView>> GetListAsync(
        string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single saved view by identifier.
    /// </summary>
    /// <param name="id">The saved view identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SavedView?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
