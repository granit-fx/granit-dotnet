using Granit.AI.EntityFrameworkCore.Entities;
using Granit.AI.Workspaces;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IAIWorkspaceStoreReader"/> and <see cref="IAIWorkspaceStoreWriter"/>.
/// </summary>
/// <remarks>
/// Reads are implicitly scoped to the current tenant via the <see cref="IMultiTenant"/> query
/// filter applied by <c>ApplyGranitConventions</c> on <see cref="AIDbContext"/>, and filter
/// out deactivated workspaces via the <see cref="IActive"/> filter. Admin write paths
/// (Update, Delete) bypass the <see cref="IActive"/> filter so a deactivated workspace can
/// still be reactivated or removed by name.
/// </remarks>
internal sealed class EfAIWorkspaceStore(
    IDbContextFactory<AIDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter)
    : EfStoreBase<AIWorkspaceEntity, AIDbContext>(contextFactory, currentTenant), IAIWorkspaceStoreReader, IAIWorkspaceStoreWriter
{
    /// <inheritdoc/>
    public async Task<AIWorkspace?> FindAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        // Admin surface — also see deactivated workspaces so they can be reactivated.
        using (dataFilter.Disable<IActive>())
        {
            AIWorkspaceEntity? entity = await FirstOrDefaultAsync(
                w => w.Name == workspaceName, cancellationToken).ConfigureAwait(false);

            return entity?.ToRecord();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIWorkspace>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using (dataFilter.Disable<IActive>())
        {
            IReadOnlyList<AIWorkspaceEntity> entities = await ListAsync(
                Spec.For<AIWorkspaceEntity>()
                    .OrderBy(w => (object)w.Name),
                cancellationToken).ConfigureAwait(false);

            return entities.Select(e => e.ToRecord()).ToList();
        }
    }

    /// <inheritdoc/>
    public async Task CreateAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var entity = AIWorkspaceEntity.FromRecord(workspace);

        try
        {
            await AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            throw new InvalidOperationException(
                $"A workspace named '{workspace.Name}' already exists.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        using (dataFilter.Disable<IActive>())
        {
            await WriteAsync(async db =>
            {
                AIWorkspaceEntity? entity = await db.Workspaces
                    .FirstOrDefaultAsync(w => w.Name == workspace.Name, cancellationToken).ConfigureAwait(false);

                if (entity is null)
                {
                    return;
                }

                entity.Provider = workspace.Provider;
                entity.Model = workspace.Model;
                entity.SystemPrompt = workspace.SystemPrompt;
                entity.Temperature = workspace.Temperature;
                entity.MaxOutputTokens = workspace.MaxOutputTokens;
                entity.Activated = workspace.Activated;
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        using (dataFilter.Disable<IActive>())
        {
            await WriteAsync(async db =>
            {
                AIWorkspaceEntity? entity = await db.Workspaces
                    .FirstOrDefaultAsync(w => w.Name == workspaceName, cancellationToken).ConfigureAwait(false);

                if (entity is not null)
                {
                    db.Workspaces.Remove(entity);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
