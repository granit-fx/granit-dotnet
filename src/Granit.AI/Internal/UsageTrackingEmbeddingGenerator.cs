using Granit.AI.Diagnostics;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;

namespace Granit.AI.Internal;

/// <summary>
/// Middleware that stamps an <see cref="AIUsageRecord"/> (and token metrics) for every
/// embedding generation flowing through a factory-created generator. Applied by
/// <see cref="DefaultAIEmbeddingGeneratorFactory"/> — the embedding twin of
/// <see cref="UsageTrackingChatClient"/>.
/// </summary>
internal sealed class UsageTrackingEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> inner,
    string workspaceName,
    AIWorkspace workspace,
    IAIUsageTracker usageTracker,
    IAIUsageRecordFactory usageRecordFactory,
    AIMetrics metrics,
    TimeProvider timeProvider) : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
{
    private static readonly TimeSpan StampTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        long startTimestamp = timeProvider.GetTimestamp();

        GeneratedEmbeddings<Embedding<float>> embeddings = await base
            .GenerateAsync(values, options, cancellationToken)
            .ConfigureAwait(false);

        if (embeddings.Usage is { } usage)
        {
            int inputTokens = (int)(usage.InputTokenCount ?? 0);

            metrics.RecordTokensUsed(
                workspace.TenantId?.ToString(), workspace.Model, workspace.Provider, inputTokens, outputTokens: 0);

            AIUsageRecord record = usageRecordFactory.Create(
                workspaceName,
                workspace.Provider,
                workspace.Model,
                inputTokens,
                outputTokens: 0,
                timeProvider.GetElapsedTime(startTimestamp));

            // Detached token: the caller's cancellation must not cancel the usage write.
            using CancellationTokenSource usageCts = new(StampTimeout);
            await usageTracker.RecordAsync(record, usageCts.Token).ConfigureAwait(false);
        }

        return embeddings;
    }
}
