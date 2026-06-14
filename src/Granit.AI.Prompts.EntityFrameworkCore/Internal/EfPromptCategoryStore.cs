using Granit.AI.Prompts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IPromptCategoryStore"/>. Categories are tenant-scoped (the tenant filter is
/// applied by the DbContext).
/// </summary>
internal sealed class EfPromptCategoryStore(IDbContextFactory<AIPromptsDbContext> contextFactory) : IPromptCategoryStore
{
    public async Task<PromptCategory> CreateAsync(PromptCategory category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.PromptCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return category;
    }

    public async Task<PromptCategory?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PromptCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PromptCategory>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
