using Granit.AI.EntityFrameworkCore.Entities;
using Granit.AI.Workspaces;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IAIWorkspaceStoreReader"/> and <see cref="IAIWorkspaceStoreWriter"/>.
/// </summary>
/// <remarks>
/// All reads are implicitly scoped to the current tenant via the <see cref="IMultiTenant"/>
/// query filter applied by <c>ApplyGranitConventions</c> on <see cref="AIDbContext"/>.
/// </remarks>
internal sealed class EfAIWorkspaceStore(
    IDbContextFactory<AIDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<AIWorkspaceEntity, AIDbContext>(contextFactory, currentTenant), IAIWorkspaceStoreReader, IAIWorkspaceStoreWriter
{
    /// <inheritdoc/>
    public async Task<AIWorkspace?> FindAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        AIWorkspaceEntity? entity = await FirstOrDefaultAsync(
            w => w.Name == workspaceName && w.IsActive, cancellationToken).ConfigureAwait(false);

        return entity?.ToRecord();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIWorkspace>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AIWorkspaceEntity> entities = await ListAsync(
            Spec.For<AIWorkspaceEntity>()
                .Where(w => w.IsActive)
                .OrderBy(w => (object)w.Name),
            cancellationToken).ConfigureAwait(false);

        return entities.Select(e => e.ToRecord()).ToList();
    }

    /// <inheritdoc/>
    public async Task CreateAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var entity = AIWorkspaceEntity.FromRecord(workspace);
        await AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
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
            entity.IsActive = workspace.IsActive;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
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
