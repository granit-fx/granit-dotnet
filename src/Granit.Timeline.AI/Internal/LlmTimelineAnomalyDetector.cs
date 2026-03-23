using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.Querying;
using Granit.Timeline.Abstractions;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="ITimelineAnomalyDetector"/>.
/// Fetches timeline entries via <see cref="ITimelineReader"/> and asks the LLM to detect
/// unusual patterns such as bulk edits, off-hours activity, and privilege escalation.
/// </summary>
internal sealed partial class LlmTimelineAnomalyDetector(
    IAIChatClientFactory chatClientFactory,
    ITimelineReader timelineReader,
    IOptions<TimelineAIOptions> options,
    TimelineAIMetrics metrics,
    ILogger<LlmTimelineAnomalyDetector> logger) : ITimelineAnomalyDetector
{
    private static readonly AnomalyReport NoAnomalies = new(HasAnomalies: false, Anomalies: []);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<AnomalyReport> DetectAnomaliesAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        long startTimestamp = Stopwatch.GetTimestamp();
        TimelineAIOptions config = options.Value;

        List<TimelineStreamEntry> entries = await FetchEntriesAsync(
            entityType, entityId, config.MaxEntriesToAnalyze, ct).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return NoAnomalies;
        }

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, ct)
                .ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            string prompt = BuildAnomalyDetectionPrompt(entityType, entityId, entries);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            AnomalyReport report = ParseAnomalyResponse(responseText);

            metrics.RecordAnomalyDetectionCompleted(tenantId: null, entityType);
            metrics.RecordAnomalyDetectionDuration(tenantId: null, entityType, Stopwatch.GetElapsedTime(startTimestamp));

            if (report.HasAnomalies)
            {
                metrics.RecordAnomaliesFound(tenantId: null, entityType, report.Anomalies.Count);
            }

            return report;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            metrics.RecordAnomalyDetectionFailure(tenantId: null, entityType);
            LogAnomalyDetectionTimeout(logger, entityType, entityId, config.TimeoutSeconds);
            return NoAnomalies;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.RecordAnomalyDetectionFailure(tenantId: null, entityType);
            LogAnomalyDetectionFailure(logger, entityType, entityId, ex);
            return NoAnomalies;
        }
    }

    private async Task<List<TimelineStreamEntry>> FetchEntriesAsync(
        string entityType,
        Guid entityId,
        int maxEntries,
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

    internal static string BuildAnomalyDetectionPrompt(
        string entityType,
        Guid entityId,
        List<TimelineStreamEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Analyze the following activity timeline for {entityType} '{entityId}' and detect any anomalies.");
        sb.AppendLine("Look for: bulk edits in short time windows, off-hours activity (outside 06:00-22:00 UTC), privilege escalation patterns, unusual author patterns, rapid state changes, and any other suspicious behavior.");
        sb.AppendLine();
        sb.AppendLine("Return JSON only, no markdown fences:");
        sb.AppendLine("""{"anomalies": [{"description": "<string>", "severity": "Low|Medium|High"}]}""");
        sb.AppendLine("Return an empty array if no anomalies are detected.");
        sb.AppendLine();
        sb.AppendLine("Timeline entries (newest first):");

        foreach (TimelineStreamEntry entry in entries)
        {
            string author = entry.AuthorName ?? entry.AuthorId ?? "System";
            sb.AppendLine($"- [{entry.OccurredAt:u}] ({entry.EntryType}) {author}: {entry.Body}");
        }

        return sb.ToString();
    }

    internal static AnomalyReport ParseAnomalyResponse(string responseText)
    {
        string trimmed = responseText.Trim();

        // Strip markdown code fences if the LLM wraps the JSON anyway
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        try
        {
            AnomalyResponseJson? parsed = JsonSerializer.Deserialize<AnomalyResponseJson>(trimmed, JsonOptions);

            if (parsed?.Anomalies is null || parsed.Anomalies.Count == 0)
            {
                return NoAnomalies;
            }

            var anomalies = parsed.Anomalies
                .Where(a => !string.IsNullOrWhiteSpace(a.Description))
                .Select(a => new TimelineAnomaly(
                    a.Description!,
                    NormalizeSeverity(a.Severity)))
                .ToList();

            return new AnomalyReport(anomalies.Count > 0, anomalies);
        }
        catch (JsonException)
        {
            return NoAnomalies;
        }
    }

    private static string NormalizeSeverity(string? severity) =>
        severity?.Trim().ToUpperInvariant() switch
        {
            "LOW" => "Low",
            "MEDIUM" => "Medium",
            "HIGH" => "High",
            _ => "Low",
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline anomaly detection timed out after {TimeoutSeconds}s for {EntityType} '{EntityId}'")]
    private static partial void LogAnomalyDetectionTimeout(ILogger logger, string entityType, Guid entityId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline anomaly detection failed for {EntityType} '{EntityId}'")]
    private static partial void LogAnomalyDetectionFailure(ILogger logger, string entityType, Guid entityId, Exception exception);

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    private sealed record AnomalyResponseJson(List<AnomalyItemJson>? Anomalies);

    /// <summary>
    /// Internal DTO for a single anomaly in the LLM JSON response.
    /// </summary>
    private sealed record AnomalyItemJson(string? Description, string? Severity);
}
