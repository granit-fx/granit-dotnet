using System.Diagnostics;
using System.Text;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="ITimelineSummarizer"/>.
/// Fetches timeline entries via <see cref="ITimelineReader"/> and asks the LLM to produce
/// a concise natural language summary.
/// </summary>
internal sealed partial class LlmTimelineSummarizer(
    IAIChatClientFactory chatClientFactory,
    ITimelineReader timelineReader,
    IOptions<TimelineAIOptions> options,
    ICurrentTenant currentTenant,
    TimelineAIMetrics metrics,
    ILogger<LlmTimelineSummarizer> logger) : ITimelineSummarizer
{
    // VULN-103: Shared concurrency limiter to prevent denial-of-wallet via unbounded LLM calls
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(3, 3);

    private static readonly TimelineSummary EmptySummary = new(
        Text: "No timeline entries found.",
        EntryCount: 0,
        OldestEntry: null,
        NewestEntry: null);

    /// <inheritdoc/>
    public async Task<TimelineSummary> SummarizeAsync(
        string entityType,
        Guid entityId,
        DateTimeOffset? since = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        long startTimestamp = Stopwatch.GetTimestamp();
        TimelineAIOptions config = options.Value;

        List<TimelineStreamEntry> entries = await FetchEntriesAsync(
            entityType, entityId, config.MaxEntriesToAnalyze, since, ct).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return EmptySummary;
        }

        // VULN-103: Concurrency limiter to prevent denial-of-wallet
        await ConcurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, ct)
                .ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            string prompt = BuildSummarizationPrompt(entityType, entityId, entries);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string text = response.Text ?? "Unable to generate summary.";

            // Entries are sorted newest-first by ITimelineReader
            DateTimeOffset newest = entries[0].OccurredAt;
            DateTimeOffset oldest = entries[^1].OccurredAt;

            metrics.RecordSummarizationCompleted(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            metrics.RecordSummarizationDuration(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType, Stopwatch.GetElapsedTime(startTimestamp));

            return new TimelineSummary(text, entries.Count, oldest, newest);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            metrics.RecordSummarizationFailure(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            LogSummarizationTimeout(logger, entityType, entityId, config.TimeoutSeconds);
            return new TimelineSummary(
                "Summary generation timed out.",
                entries.Count,
                entries[^1].OccurredAt,
                entries[0].OccurredAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.RecordSummarizationFailure(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            LogSummarizationFailure(logger, entityType, entityId, ex);
            return new TimelineSummary(
                "Summary generation failed.",
                entries.Count,
                entries[^1].OccurredAt,
                entries[0].OccurredAt);
        }
        finally
        {
            ConcurrencyLimiter.Release();
        }
    }

    private async Task<List<TimelineStreamEntry>> FetchEntriesAsync(
        string entityType,
        Guid entityId,
        int maxEntries,
        DateTimeOffset? since,
        CancellationToken ct)
    {
        var allEntries = new List<TimelineStreamEntry>();
        int page = 1;
        const int pageSize = 50;

        while (allEntries.Count < maxEntries)
        {
            PagedResult<TimelineStreamEntry> result = await timelineReader
                .GetStreamAsync(entityType, entityId.ToString(), page, pageSize, ct)
                .ConfigureAwait(false);

            if (result.Items.Count == 0)
            {
                break;
            }

            foreach (TimelineStreamEntry entry in result.Items)
            {
                if (since.HasValue && entry.OccurredAt < since.Value)
                {
                    return allEntries;
                }

                // VULN-102: Exclude staff-only InternalNote entries from LLM prompts
                if (entry.EntryType == TimelineStreamEntryType.InternalNote)
                {
                    continue;
                }

                allEntries.Add(entry);

                if (allEntries.Count >= maxEntries)
                {
                    return allEntries;
                }
            }

            if (!result.HasMore)
            {
                break;
            }

            page++;
        }

        return allEntries;
    }

    internal static string BuildSummarizationPrompt(
        string entityType,
        Guid entityId,
        List<TimelineStreamEntry> entries)
    {
        var pb = new PromptBuilder(maxInputLength: 50_000);

        pb.AppendInstruction($"Summarize the following activity timeline for {entityType} '{entityId}' in 2-4 concise sentences.");
        pb.AppendInstruction("Focus on key events, who did what, and the overall progression. Be factual and concise.");
        pb.AppendInstruction(string.Empty);

        var sb = new StringBuilder();
        foreach (TimelineStreamEntry entry in entries)
        {
            // VULN-002: Pseudonymize PII — never send AuthorName ([SensitiveData]) to external LLM
            string author = entry.AuthorId is { Length: >= 8 } id ? $"User-{id[..8]}" : "System";
            sb.AppendLine($"- [{entry.OccurredAt:u}] ({entry.EntryType}) {author}: {entry.Body}");
        }

        pb.AppendUserTextBlock("Timeline entries (newest first)", sb.ToString());

        return pb.Build();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline summarization timed out after {TimeoutSeconds}s for {EntityType} '{EntityId}'")]
    private static partial void LogSummarizationTimeout(ILogger logger, string entityType, Guid entityId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline summarization failed for {EntityType} '{EntityId}'")]
    private static partial void LogSummarizationFailure(ILogger logger, string entityType, Guid entityId, Exception exception);
}
