using Granit.AI.Chat.BackgroundJobs.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Chat.BackgroundJobs.Internal;

/// <summary>
/// Purges conversations whose last activity is older than the configured retention window. Batching
/// is handled by <see cref="IConversationDataManager.PurgeOlderThanAsync"/> (a fresh DbContext per
/// batch). A no-op when <see cref="GranitAIChatRetentionOptions.RetentionDays"/> is 0.
/// </summary>
internal sealed partial class ConversationRetentionService(
    IConversationDataManager dataManager,
    IOptions<GranitAIChatRetentionOptions> options,
    IClock clock,
    ILogger<ConversationRetentionService> logger) : IConversationRetentionService
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        GranitAIChatRetentionOptions opts = options.Value;
        if (opts.RetentionDays <= 0)
        {
            LogDisabled();
            return;
        }

        DateTimeOffset cutoff = clock.Now - TimeSpan.FromDays(opts.RetentionDays);
        int purged = await dataManager
            .PurgeOlderThanAsync(cutoff, opts.CleanupBatchSize, cancellationToken)
            .ConfigureAwait(false);

        LogPurged(purged, cutoff);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Conversation retention disabled (RetentionDays = 0); nothing purged")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Conversation retention purged {Count} conversation(s) (cutoff: {Cutoff})")]
    private partial void LogPurged(int count, DateTimeOffset cutoff);
}
