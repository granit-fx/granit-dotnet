using System.Runtime.CompilerServices;
using Granit.AI.Diagnostics;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Granit.AI.Internal;

/// <summary>
/// Middleware that stamps an <see cref="AIUsageRecord"/> (and token metrics) for every model
/// round-trip flowing through a factory-created <see cref="IChatClient"/>. Applied by
/// <see cref="DefaultAIChatClientFactory"/> so no caller can forget usage tracking.
/// </summary>
/// <remarks>
/// One record per underlying model call: inside an agentic loop the decorator sits below
/// <c>FunctionInvokingChatClient</c>, so each round-trip of a turn stamps its own record —
/// they share the run's <see cref="AIUsageContext.ConversationId"/>. Streaming responses
/// harvest the last <see cref="UsageContent"/> and stamp in a <c>finally</c>, so an aborted
/// stream still records; the caller's <see cref="CancellationToken"/> never reaches the
/// tracker (an aborted request would cancel the usage write itself — a billing hole).
/// </remarks>
internal sealed partial class UsageTrackingChatClient(
    IChatClient inner,
    string workspaceName,
    AIWorkspace workspace,
    IAIUsageTracker usageTracker,
    IAIUsageRecordFactory usageRecordFactory,
    AIMetrics metrics,
    TimeProvider timeProvider,
    ILogger<UsageTrackingChatClient> logger) : DelegatingChatClient(inner)
{
    private static readonly TimeSpan StampTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        long startTimestamp = timeProvider.GetTimestamp();

        ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        if (response.Usage is { } usage)
        {
            await StampAsync(usage, timeProvider.GetElapsedTime(startTimestamp)).ConfigureAwait(false);
        }

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long startTimestamp = timeProvider.GetTimestamp();
        UsageDetails? lastUsage = null;

        try
        {
            await foreach (ChatResponseUpdate update in base
                .GetStreamingResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false))
            {
                foreach (AIContent content in update.Contents)
                {
                    if (content is UsageContent usageContent)
                    {
                        lastUsage = usageContent.Details;
                    }
                }

                yield return update;
            }
        }
        finally
        {
            if (lastUsage is not null)
            {
                // Never mask the streaming exception (or swallow a caller abort) with a
                // tracker failure — usage loss is logged, not thrown.
                try
                {
                    await StampAsync(lastUsage, timeProvider.GetElapsedTime(startTimestamp)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogStampFailed(ex, workspaceName);
                }
            }
        }
    }

    private async Task StampAsync(UsageDetails usage, TimeSpan duration)
    {
        int inputTokens = (int)(usage.InputTokenCount ?? 0);
        int outputTokens = (int)(usage.OutputTokenCount ?? 0);

        metrics.RecordTokensUsed(
            workspace.TenantId?.ToString(), workspace.Model, workspace.Provider, inputTokens, outputTokens);

        AIUsageRecord record = usageRecordFactory.Create(
            workspaceName, workspace.Provider, workspace.Model, inputTokens, outputTokens, duration);

        // Detached token: stamp even when the caller aborted, but cap the write so a stuck
        // sink cannot leak an orphaned task.
        using CancellationTokenSource usageCts = new(StampTimeout);
        await usageTracker.RecordAsync(record, usageCts.Token).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to stamp the AI usage record for workspace '{WorkspaceName}' after a streamed response.")]
    private partial void LogStampFailed(Exception exception, string workspaceName);
}
