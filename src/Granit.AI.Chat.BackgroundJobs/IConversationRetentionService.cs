namespace Granit.AI.Chat.BackgroundJobs;

/// <summary>
/// Purges conversations past the configured retention window. Driven by the recurring
/// <see cref="Jobs.ConversationRetentionCleanupJob"/>; a no-op when retention is disabled.
/// </summary>
public interface IConversationRetentionService
{
    /// <summary>Purges conversations older than the configured retention window.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
