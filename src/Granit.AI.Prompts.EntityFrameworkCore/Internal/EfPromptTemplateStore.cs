using Granit.AI.Prompts.Domain;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IPromptTemplateStore"/>. Reads are scoped to the caller's catalogue (system
/// prompts plus their own); the tenant filter is applied by the DbContext.
/// </summary>
internal sealed class EfPromptTemplateStore(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    IGuidGenerator guidGenerator) : IPromptTemplateStore
{
    public async Task<PromptTemplate> CreateAsync(PromptTemplate prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.PromptTemplates.Add(prompt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return prompt;
    }

    public async Task<PromptTemplate?> GetAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptTemplates
            .AsNoTracking()
            .Include(p => p.CategoryLinks)
            .FirstOrDefaultAsync(p => p.Id == id && (p.IsSystem || p.OwnerId == ownerId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PromptTemplate>> ListCatalogueAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptTemplates
            .AsNoTracking()
            .Include(p => p.CategoryLinks)
            .Where(p => p.IsSystem || p.OwnerId == ownerId)
            .OrderByDescending(p => p.IsSystem)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PromptTemplate?> UpdateAsync(Guid id, Guid ownerId, PromptTemplateEdit edit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // No Include: the existing links are rebuilt below, so leaving them out of the navigation keeps
        // EF's relationship fixup from resurrecting the rows we delete.
        PromptTemplate? prompt = await context.PromptTemplates
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId && !p.IsSystem, cancellationToken)
            .ConfigureAwait(false);

        if (prompt is null)
        {
            return null;
        }

        prompt.Edit(edit.Name, edit.ShortDescription, edit.Content, edit.Icon, edit.IconColor);

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Rebuild category membership: delete the old links directly (a severed required FK is marked
        // Modified, not Deleted, by the tracker), then re-add the desired set through the aggregate.
        await context.Set<PromptTemplateCategory>()
            .Where(l => l.PromptTemplateId == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (Guid categoryId in edit.CategoryIds.Distinct())
        {
            prompt.AssignCategory(guidGenerator.Create(), categoryId);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await context.PromptTemplates
            .AsNoTracking()
            .Include(p => p.CategoryLinks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PromptTemplate? prompt = await context.PromptTemplates
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId && !p.IsSystem, cancellationToken)
            .ConfigureAwait(false);

        if (prompt is null)
        {
            return false;
        }

        context.PromptTemplates.Remove(prompt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
