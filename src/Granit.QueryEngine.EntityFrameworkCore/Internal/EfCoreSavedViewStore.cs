using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ISavedViewStoreReader"/> and <see cref="ISavedViewStoreWriter"/>.
/// Performs CRUD operations on <see cref="SavedView"/> via <see cref="QueryEngineDbContext"/>.
/// </summary>
internal sealed class EfCoreSavedViewStore(
    IDbContextFactory<QueryEngineDbContext> contextFactory) : ISavedViewStoreReader, ISavedViewStoreWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavedView>> GetListAsync(
        string entityType, string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.SavedViews
            .AsNoTracking()
            .Where(v => v.EntityType == entityType
                && v.TenantId == tenantId
                && (v.UserId == userId || v.IsShared))
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SavedView?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.SavedViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(SavedView view, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.SavedViews.Add(view);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(SavedView view, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.SavedViews.Update(view);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        SavedView? view = await context.SavedViews
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);

        if (view is not null)
        {
            context.SavedViews.Remove(view);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SetDefaultAsync(
        Guid id, string userId, string entityType, CancellationToken cancellationToken = default)
    {
        await using QueryEngineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Unset any previous default for the same user and entity type
        List<SavedView> previousDefaults = await context.SavedViews
            .Where(v => v.EntityType == entityType
                && v.UserId == userId
                && v.IsDefault)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (SavedView previous in previousDefaults)
        {
            previous.IsDefault = false;
        }

        // Set the new default
        SavedView? target = await context.SavedViews
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);

        if (target is not null)
        {
            target.IsDefault = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
