using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
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
/// LLM-based implementation of <see cref="ITimelineAnomalyDetector"/>.
/// Fetches timeline entries via <see cref="ITimelineReader"/> and asks the LLM to detect
/// unusual patterns such as bulk edits, off-hours activity, and privilege escalation.
/// </summary>
internal sealed partial class LlmTimelineAnomalyDetector(
    IAIChatClientFactory chatClientFactory,
    ITimelineReader timelineReader,
    IOptions<TimelineAIOptions> options,
    ICurrentTenant currentTenant,
    TimelineAIMetrics metrics,
    ILogger<LlmTimelineAnomalyDetector> logger) : ITimelineAnomalyDetector
{
    // VULN-103: Shared concurrency limiter to prevent denial-of-wallet via unbounded LLM calls
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(3, 3);

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

        // VULN-103: Concurrency limiter to prevent denial-of-wallet
        await ConcurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
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

            metrics.RecordAnomalyDetectionCompleted(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            metrics.RecordAnomalyDetectionDuration(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType, Stopwatch.GetElapsedTime(startTimestamp));

            if (report.HasAnomalies)
            {
                metrics.RecordAnomaliesFound(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType, report.Anomalies.Count);
            }

            return report;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            metrics.RecordAnomalyDetectionFailure(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            LogAnomalyDetectionTimeout(logger, entityType, entityId, config.TimeoutSeconds);
            return NoAnomalies;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.RecordAnomalyDetectionFailure(tenantId: currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null, entityType);
            LogAnomalyDetectionFailure(logger, entityType, entityId, ex);
            return NoAnomalies;
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

    internal static string BuildAnomalyDetectionPrompt(
        string entityType,
        Guid entityId,
        List<TimelineStreamEntry> entries)
    {
        var pb = new PromptBuilder(maxInputLength: 50_000);

        pb.AppendInstruction($"Analyze the following activity timeline for {entityType} '{entityId}' and detect any anomalies.");
        pb.AppendInstruction("Look for: bulk edits in short time windows, off-hours activity (outside 06:00-22:00 UTC), privilege escalation patterns, unusual author patterns, rapid state changes, and any other suspicious behavior.");
        pb.AppendInstruction(string.Empty);
        pb.AppendInstruction("""
            Return JSON only, no markdown fences:
            {"anomalies": [{"description": "<string>", "severity": "Low|Medium|High"}]}
            Return an empty array if no anomalies are detected.
            """);

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

    internal static AnomalyReport ParseAnomalyResponse(string responseText)
    {
        string trimmed = LlmResponseHelper.StripMarkdownCodeFences(responseText);

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
