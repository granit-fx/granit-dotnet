using Granit.AI.Prompts.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IPromptTemplateDataManager"/>. Take-out reads respect the ambient tenant scope;
/// erasure bypasses every query filter (soft-delete and tenant) to guarantee a true physical removal
/// of the subject's prompts. System prompts (<see cref="PromptTemplate.IsSystem"/>) are never read or
/// erased — they are framework seeds, not personal data.
/// </summary>
internal sealed class EfPromptTemplateDataManager(IDbContextFactory<AIPromptsDbContext> contextFactory)
    : IPromptTemplateDataManager
{
    public async Task<IReadOnlyList<PromptTemplate>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.PromptTemplates
            .AsNoTracking()
            .Include(p => p.CategoryLinks)
            .Where(p => p.OwnerId == ownerId && !p.IsSystem)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> EraseOwnerAsync(Guid? tenantId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIPromptsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<Guid> ids = await context.PromptTemplates
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(p => p.OwnerId == ownerId && !p.IsSystem && (tenantId == null || p.TenantId == tenantId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (ids.Count == 0)
        {
            return 0;
        }

        // Category links are owned by the aggregate (no soft-delete); remove them first to clear the FK.
        await context.Set<PromptTemplateCategory>()
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(l => ids.Contains(l.PromptTemplateId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.PromptTemplates
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(p => ids.Contains(p.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
