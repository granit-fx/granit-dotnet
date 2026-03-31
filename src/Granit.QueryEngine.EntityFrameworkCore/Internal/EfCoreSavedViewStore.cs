using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISavedViewStoreReader"/> and <see cref="ISavedViewStoreWriter"/>.
/// Performs CRUD operations on <see cref="SavedView"/> via <see cref="QueryEngineDbContext"/>.
/// </summary>
internal sealed class EfCoreSavedViewStore(
    IDbContextFactory<QueryEngineDbContext> contextFactory)
    : EfStoreBase<SavedView, QueryEngineDbContext>(contextFactory), ISavedViewStoreReader, ISavedViewStoreWriter
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedView>> GetListAsync(
        string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<SavedView>()
                .Where(v => v.EntityType == entityType
                    && v.TenantId == tenantId
                    && (v.UserId == userId || v.IsShared))
                .OrderBy(v => v.Name),
            cancellationToken);

    /// <inheritdoc/>
    public Task<int> GetCountAsync(
        string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        CountAsync(v => v.EntityType == entityType
            && v.UserId == userId
            && v.TenantId == tenantId, cancellationToken);

    /// <inheritdoc/>
    public Task<SavedView?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task CreateAsync(SavedView view, CancellationToken cancellationToken = default) =>
        AddAsync(view, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(SavedView view, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(view, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            SavedView? view = await db.SavedViews
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);

            if (view is not null)
            {
                db.SavedViews.Remove(view);
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public Task SetDefaultAsync(
        Guid id, string userId, string entityType, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            // Unset any previous default for the same user and entity type
            List<SavedView> previousDefaults = await db.SavedViews
                .Where(v => v.EntityType == entityType
                    && v.UserId == userId
                    && v.IsDefault)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (SavedView previous in previousDefaults)
            {
                previous.IsDefault = false;
            }

            // Set the new default
            SavedView? target = await db.SavedViews
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);

            if (target is not null)
            {
                target.IsDefault = true;
            }
        }, cancellationToken);
}
