using Granit.AI.Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IConversationStore"/>. Every query is scoped to the owner (and the tenant,
/// via the DbContext filter), so a caller can only reach their own conversations.
/// </summary>
internal sealed class EfConversationStore(IDbContextFactory<AIChatDbContext> contextFactory) : IConversationStore
{
    public async Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return conversation;
    }

    public async Task<Conversation?> GetAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Conversation>> ListAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Conversations
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> AppendMessagesAsync(
        Guid id, Guid ownerId, IReadOnlyList<Message> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        bool owned = await context.Conversations
            .AnyAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (!owned)
        {
            return false;
        }

        context.Set<Message>().AddRange(messages);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RenameAsync(Guid id, Guid ownerId, string title, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Conversation? conversation = await context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return false;
        }

        conversation.Rename(title);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await using AIChatDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Conversation? conversation = await context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return false;
        }

        // Soft delete (FullAudited) via the SoftDeleteInterceptor.
        context.Conversations.Remove(conversation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
