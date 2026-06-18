using Granit.AI.Chat.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IConversationDataManager"/>. Take-out reads respect the ambient tenant scope;
/// erasure and retention bypass every query filter (soft-delete and tenant) to guarantee a true
/// physical removal of personal data.
/// </summary>
internal sealed class EfConversationDataManager(IDbContextFactory<AIChatDbContext> contextFactory) : IConversationDataManager
{
    public async Task<IReadOnlyList<Conversation>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Conversations
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            .OrderBy(c => c.CreatedAt)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> EraseOwnerAsync(Guid? tenantId, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<Guid> ids = await context.Conversations
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(c => c.OwnerId == ownerId && (tenantId == null || c.TenantId == tenantId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (ids.Count == 0)
        {
            return 0;
        }

        await context.Set<Message>()
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(m => ids.Contains(m.ConversationId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // Reports carry the owner's id (personal data) — erase the owner's reports too (GDPR).
        await context.MessageReports
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(r => r.OwnerId == ownerId && (tenantId == null || r.TenantId == tenantId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Conversations
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
            .Where(c => ids.Contains(c.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default)
    {
        int total = 0;
        int batch;
        do
        {
            await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            List<Guid> ids = await context.Conversations
                .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
                .Where(c => (c.Messages.Max(m => (DateTimeOffset?)m.CreatedAt) ?? c.CreatedAt) < cutoff)
                .OrderBy(c => c.CreatedAt)
                .Take(batchSize)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (ids.Count == 0)
            {
                break;
            }

            await context.Set<Message>()
                .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
                .Where(m => ids.Contains(m.ConversationId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // Purge reports for the conversations being retired so no personal data outlives them.
            await context.MessageReports
                .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
                .Where(r => ids.Contains(r.ConversationId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            batch = await context.Conversations
                .IgnoreQueryFilters([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant])
                .Where(c => ids.Contains(c.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            total += batch;
        }
        while (batch == batchSize);

        return total;
    }
}
