using Granit.AI.EntityFrameworkCore.Entities;
using Granit.AI.Workspaces;
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
    IDbContextFactory<AIDbContext> contextFactory) : IAIWorkspaceStoreReader, IAIWorkspaceStoreWriter
{
    /// <inheritdoc/>
    public async Task<AIWorkspace?> FindAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        AIWorkspaceEntity? entity = await context.Workspaces
            .FirstOrDefaultAsync(w => w.Name == workspaceName && w.IsActive, cancellationToken).ConfigureAwait(false);

        return entity?.ToRecord();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIWorkspace>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<AIWorkspaceEntity> entities = await context.Workspaces
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.ConvertAll(e => e.ToRecord());
    }

    /// <inheritdoc/>
    public async Task SaveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = AIWorkspaceEntity.FromRecord(workspace);
        context.Workspaces.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        AIWorkspaceEntity? entity = await context.Workspaces
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string workspaceName,
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        AIWorkspaceEntity? entity = await context.Workspaces
            .FirstOrDefaultAsync(w => w.Name == workspaceName, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            context.Workspaces.Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
