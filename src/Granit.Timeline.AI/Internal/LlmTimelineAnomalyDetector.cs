using System.Diagnostics;
using System.Text;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.AI.Diagnostics;
using Granit.Timeline.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="ITimelineAnomalyDetector"/> built on the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). Fetches timeline entries via
/// <see cref="ITimelineReader"/> and asks the model to detect unusual patterns such as bulk
/// edits, off-hours activity, and privilege escalation. Fail-soft: any unavailable response
/// yields no anomalies.
/// </summary>
#pragma warning disable CA1001 // Lifetime managed by DI container — SemaphoreSlim does not hold unmanaged resources
internal sealed partial class LlmTimelineAnomalyDetector(
    IStructuredCompletion structuredCompletion,
    ITimelineReader timelineReader,
    IOptions<TimelineAIOptions> options,
    ICurrentTenant currentTenant,
    TimelineAIMetrics metrics,
    ILogger<LlmTimelineAnomalyDetector> logger) : ITimelineAnomalyDetector
#pragma warning restore CA1001
{
    // Concurrency limiter to prevent denial-of-wallet via unbounded LLM calls.
    private readonly SemaphoreSlim _concurrencyLimiter = new(
        options.Value.MaxConcurrentRequests, options.Value.MaxConcurrentRequests);

    private static readonly AnomalyReport NoAnomalies = new(HasAnomalies: false, Anomalies: []);

    /// <inheritdoc/>
    public async Task<AnomalyReport> DetectAnomaliesAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        long startTimestamp = Stopwatch.GetTimestamp();
        TimelineAIOptions config = options.Value;
        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;

        List<TimelineStreamEntry> entries = await FetchEntriesAsync(
            entityType, entityId, config.AnomalyDetectorMaxEntries, ct).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return NoAnomalies;
        }

        await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var request = new StructuredCompletionRequest
            {
                Instruction = BuildInstruction(entityType, entityId),
                Content = BuildEntriesBlock(entries),
                ContentLabel = "Timeline entries (newest first)",
                WorkspaceName = config.WorkspaceName,
            };

            try
            {
                StructuredCompletionResult<AnomalyResponseJson> result = await structuredCompletion
                    .CompleteAsync<AnomalyResponseJson>(request, linkedCts.Token)
                    .ConfigureAwait(false);

                if (result.Status != StructuredCompletionStatus.Succeeded)
                {
                    metrics.RecordAnomalyDetectionFailure(tenantId, entityType);
                    LogAnomalyDetectionUnavailable(logger, entityType, entityId, result.Status.ToString());
                    return NoAnomalies;
                }

                AnomalyReport report = BuildReport(result.Value!);

                metrics.RecordAnomalyDetectionCompleted(tenantId, entityType);
                metrics.RecordAnomalyDetectionDuration(tenantId, entityType, Stopwatch.GetElapsedTime(startTimestamp));
                if (report.HasAnomalies)
                {
                    metrics.RecordAnomaliesFound(tenantId, entityType, report.Anomalies.Count);
                }

                return report;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                metrics.RecordAnomalyDetectionFailure(tenantId, entityType);
                LogAnomalyDetectionTimeout(logger, entityType, entityId, config.TimeoutSeconds);
                return NoAnomalies;
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
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
            TimelineStreamResult streamResult = await timelineReader
                .GetStreamAsync(entityType, entityId.ToString(), page, pageSize, ct)
                .ConfigureAwait(false);
            PagedResult<TimelineStreamEntry> result = streamResult.Page;

            if (result.Items.Count == 0)
            {
                break;
            }

            foreach (TimelineStreamEntry entry in result.Items)
            {
                // Exclude staff-only InternalNote entries from LLM prompts.
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

    private static string BuildInstruction(string entityType, Guid entityId) =>
        $"""
         Analyze the supplied activity timeline for {entityType} '{entityId}' and detect any anomalies.
         Look for: bulk edits in short time windows, off-hours activity (outside 06:00-22:00 UTC),
         privilege escalation patterns, unusual author patterns, rapid state changes, and any other
         suspicious behavior. Each anomaly has a description and a severity of Low, Medium, or High.
         Return an empty anomalies array if none are detected.
         """;

    // Pseudonymize PII — never send AuthorName ([SensitiveData]) to the external LLM.
    internal static string BuildEntriesBlock(List<TimelineStreamEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (TimelineStreamEntry entry in entries)
        {
            string author = entry.AuthorId is { Length: >= 8 } id ? $"User-{id[..8]}" : "System";
            sb.AppendLine($"- [{entry.OccurredAt:u}] ({entry.EntryType}) {author}: {entry.Body}");
        }

        return sb.ToString();
    }

    internal static AnomalyReport BuildReport(AnomalyResponseJson parsed)
    {
        if (parsed.Anomalies is null || parsed.Anomalies.Count == 0)
        {
            return NoAnomalies;
        }

        var anomalies = parsed.Anomalies
            .Where(a => !string.IsNullOrWhiteSpace(a.Description))
            .Select(a => new TimelineAnomaly(a.Description!, NormalizeSeverity(a.Severity)))
            .ToList();

        return new AnomalyReport(anomalies.Count > 0, anomalies);
    }

    private static string NormalizeSeverity(string? severity) =>
        severity?.Trim().ToUpperInvariant() switch
        {
            "LOW" => "Low",
            "MEDIUM" => "Medium",
            "HIGH" => "High",
            _ => "Low",
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline anomaly detection unavailable ({Status}) for {EntityType} '{EntityId}'")]
    private static partial void LogAnomalyDetectionUnavailable(ILogger logger, string entityType, Guid entityId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timeline anomaly detection timed out after {TimeoutSeconds}s for {EntityType} '{EntityId}'")]
    private static partial void LogAnomalyDetectionTimeout(ILogger logger, string entityType, Guid entityId, int timeoutSeconds);

    /// <summary>Internal DTO for deserializing the LLM JSON response.</summary>
    internal sealed record AnomalyResponseJson(List<AnomalyItemJson>? Anomalies);

    /// <summary>Internal DTO for a single anomaly in the LLM JSON response.</summary>
    internal sealed record AnomalyItemJson(string? Description, string? Severity);
}
