using Granit.AI.Chat.Domain;

namespace Granit.AI.Chat;

/// <summary>
/// Privacy and data-lifecycle operations over <see cref="Conversation"/> aggregates that cross the
/// owner-scoped boundary of <see cref="IConversationStore"/> (ADR-067, GDPR). Used by the
/// <c>Granit.AI.Chat.Privacy</c> export/erasure handlers and the <c>Granit.AI.Chat.BackgroundJobs</c>
/// retention job — never by ordinary request handlers.
/// </summary>
public interface IConversationDataManager
{
    /// <summary>
    /// Returns every conversation (with its messages) owned by <paramref name="ownerId"/>, for a
    /// data take-out. Respects the ambient tenant scope.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetAllForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every message report raised by <paramref name="ownerId"/>, for a data take-out.
    /// Respects the ambient tenant scope.
    /// </summary>
    Task<IReadOnlyList<MessageReport>> GetReportsForOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently erases (hard delete, bypassing soft-delete) every conversation and message owned
    /// by <paramref name="ownerId"/>, optionally constrained to <paramref name="tenantId"/>. Returns
    /// the number of conversations removed. Idempotent.
    /// </summary>
    Task<int> EraseOwnerAsync(Guid? tenantId, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently purges, in batches, conversations whose last activity is older than
    /// <paramref name="cutoff"/> (across owners and tenants). Returns the number removed.
    /// </summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default);
}
