using System.Diagnostics;
using System.Text;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.Observability.AI.Diagnostics;
using Granit.Observability.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Observability.AI.Internal;

/// <summary>
/// Analyzes log entries via the <see cref="IStructuredCompletion"/> primitive (ADR-064) and
/// returns a structured report.
/// </summary>
/// <remarks>
/// <para><b>PII warning:</b> Log messages and exception texts are sent to the configured LLM.
/// Callers must ensure log entries are pre-sanitized if they may contain PII
/// (usernames, emails, IP addresses, tokens). Use <see cref="ObservabilityAIOptions.WorkspaceName"/>
/// to target an on-premise model (e.g., Ollama) for sensitive environments.</para>
/// <para>An unparseable model response degrades to a fallback report; a transport failure or
/// timeout is surfaced as an exception (records the failure metric first), preserving the
/// original contract.</para>
/// </remarks>
internal sealed partial class LlmLogAnalyzer(
    IStructuredCompletion structuredCompletion,
    IOptions<ObservabilityAIOptions> options,
    ObservabilityAIMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<LlmLogAnalyzer> logger) : IAILogAnalyzer
{
    /// <inheritdoc />
    public async Task<LogAnalysisReport> AnalyzeAsync(
        IReadOnlyList<LogEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return new LogAnalysisReport("No log entries to analyze.", [], 0);
        }

        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;
        ObservabilityAIOptions config = options.Value;

        IReadOnlyList<LogEntry> truncatedEntries = entries.Count > config.MaxLogEntries
            ? entries.TakeLast(config.MaxLogEntries).ToList()
            : entries;

        LogAnalyzingEntries(logger, truncatedEntries.Count, entries.Count);

        var sw = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = AnalysisInstruction,
            Content = BuildEntriesBlock(truncatedEntries),
            ContentLabel = "Log entries",
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LlmAnalysisResponse> result = await structuredCompletion
                .CompleteAsync<LlmAnalysisResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case StructuredCompletionStatus.Succeeded:
                    LogAnalysisReport report = BuildReport(result.Value!, truncatedEntries.Count);
                    sw.Stop();
                    LogAnalysisComplete(logger, report.Insights.Count, truncatedEntries.Count);
                    metrics.RecordAnalysisCompleted(tenantId, report.Insights.Count, truncatedEntries.Count);
                    metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);
                    return report;

                case StructuredCompletionStatus.ModelRefused:
                case StructuredCompletionStatus.SchemaViolation:
                    // Unparseable/refused output degrades to a fallback report (no exception).
                    sw.Stop();
                    metrics.RecordAnalysisFailed(tenantId, "parse");
                    metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);
                    return new LogAnalysisReport("AI analysis returned a non-JSON response.", [], truncatedEntries.Count);

                case StructuredCompletionStatus.TransportFailure:
                default:
                    // Transport problems are surfaced to the caller, as before.
                    sw.Stop();
                    metrics.RecordAnalysisFailed(tenantId, "error");
                    metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);
                    throw new InvalidOperationException(result.ErrorMessage ?? "AI log analysis failed.");
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            sw.Stop();
            metrics.RecordAnalysisFailed(tenantId, "timeout");
            metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);
            throw;
        }
    }

    // The former system prompt + user instruction, folded into one developer-controlled
    // instruction (the primitive sends a single user message and isolates the entries in a
    // <data> block). The schema is enforced by the primitive.
    internal const string AnalysisInstruction =
        """
        You are a log analysis assistant. Analyze the supplied log entries and produce a concise
        summary of the overall log health plus a list of insights. Each insight has a description,
        a severity of Critical, High, Medium, or Low, and a category of Anomaly, Pattern,
        Regression, Performance, ErrorSpike, or Configuration. Focus on recurring errors, anomalous
        patterns, error spikes, performance-degradation signals, and configuration issues.
        """;

    internal static string BuildEntriesBlock(IReadOnlyList<LogEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (LogEntry entry in entries)
        {
            sb.Append('[').Append(entry.Timestamp.ToString("o")).Append("] ");
            sb.Append(entry.Level).Append(": ");
            sb.AppendLine(entry.Message);

            if (entry.Exception is not null)
            {
                sb.Append("  Exception: ").AppendLine(entry.Exception);
            }
        }

        return sb.ToString();
    }

    internal static LogAnalysisReport BuildReport(LlmAnalysisResponse parsed, int totalEntries)
    {
        List<LogInsight> insights = parsed.Insights
            ?.Select(i => new LogInsight(
                i.Description ?? "Unknown",
                i.Severity ?? "Medium",
                i.Category ?? "Pattern"))
            .ToList() ?? [];

        return new LogAnalysisReport(parsed.Summary ?? "No summary available.", insights, totalEntries);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyzing {TruncatedCount} log entries (total: {TotalCount})")]
    private static partial void LogAnalyzingEntries(ILogger logger, int truncatedCount, int totalCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Log analysis complete: {InsightCount} insights from {EntryCount} entries")]
    private static partial void LogAnalysisComplete(ILogger logger, int insightCount, int entryCount);

    /// <summary>Internal DTO for deserializing the LLM JSON response.</summary>
    internal sealed class LlmAnalysisResponse
    {
        public string? Summary { get; set; }
        public List<LlmInsightResponse>? Insights { get; set; }
    }

    /// <summary>Internal DTO for deserializing an individual insight from the LLM response.</summary>
    internal sealed class LlmInsightResponse
    {
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public string? Category { get; set; }
    }
}
