using Granit.AI.Prompts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IPromptTemplateStore"/>. Reads are scoped to the caller's catalogue (system
/// prompts plus their own); the tenant filter is applied by the DbContext.
/// </summary>
internal sealed class EfPromptTemplateStore(IDbContextFactory<AIPromptsDbContext> contextFactory) : IPromptTemplateStore
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
            .FirstOrDefaultAsync(p => p.Id == id && (p.IsSystem || p.OwnerId == ownerId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PromptTemplate>> ListCatalogueAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptTemplates
            .AsNoTracking()
            .Where(p => p.IsSystem || p.OwnerId == ownerId)
            .OrderByDescending(p => p.IsSystem)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
