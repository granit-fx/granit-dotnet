using Granit.BackgroundJobs;

namespace Granit.AI.Chat.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that purges conversations past the configured retention window (GDPR data
/// minimisation). Runs <b>once cluster-wide</b> via the distributed scheduler. Default schedule is
/// daily at 03:00; override via <c>BackgroundJobs:Jobs:ai-chat-retention-cleanup</c>.
/// </summary>
[RecurringJob("0 3 * * *", "ai-chat-retention-cleanup")]
public sealed record ConversationRetentionCleanupJob : IBackgroundJob;
