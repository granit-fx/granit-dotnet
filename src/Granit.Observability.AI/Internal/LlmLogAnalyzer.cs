using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.MultiTenancy;
using Granit.Observability.AI.Diagnostics;
using Granit.Observability.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Observability.AI.Internal;

/// <summary>
/// Analyzes log entries by sending them to an LLM and parsing the structured response.
/// </summary>
/// <remarks>
/// <para><b>PII warning:</b> Log messages and exception texts are sent to the configured LLM.
/// Callers must ensure log entries are pre-sanitized if they may contain PII
/// (usernames, emails, IP addresses, tokens). Use <see cref="ObservabilityAIOptions.WorkspaceName"/>
/// to target an on-premise model (e.g., Ollama) for sensitive environments.</para>
/// </remarks>
internal sealed partial class LlmLogAnalyzer(
    IAIChatClientFactory chatClientFactory,
    IOptions<ObservabilityAIOptions> options,
    ObservabilityAIMetrics metrics,
    ICurrentTenant? currentTenant,
    ILogger<LlmLogAnalyzer> logger) : IAILogAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient client = await chatClientFactory
                .CreateAsync(config.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(truncatedEntries);

            ChatMessage[] messages =
            [
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, prompt),
            ];

            ChatResponse response = await client
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string content = response.Messages.LastOrDefault()?.Text ?? string.Empty;

            LogAnalysisReport report = ParseReport(content, truncatedEntries.Count);

            sw.Stop();
            LogAnalysisComplete(logger, report.Insights.Count, truncatedEntries.Count);
            metrics.RecordAnalysisCompleted(tenantId, report.Insights.Count, truncatedEntries.Count);
            metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);

            return report;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            metrics.RecordAnalysisFailed(tenantId, "error");
            metrics.RecordAnalysisDuration(tenantId, sw.Elapsed.TotalSeconds);
            throw;
        }
    }

    private const string SystemPrompt =
        """
        You are a log analysis assistant. Analyze the provided log entries and return a JSON object with this exact structure:
        {
          "summary": "A concise summary of the overall log health and key findings",
          "insights": [
            {
              "description": "Description of the finding",
              "severity": "Critical|High|Medium|Low",
              "category": "Anomaly|Pattern|Regression|Performance|ErrorSpike|Configuration"
            }
          ]
        }
        Return ONLY valid JSON. No markdown, no explanation, no code fences.
        Focus on: recurring errors, anomalous patterns, error spikes, performance degradation signals, and configuration issues.
        """;

    internal static string BuildPrompt(IReadOnlyList<LogEntry> entries)
    {
        var pb = new PromptBuilder(maxInputLength: 100_000);

        pb.AppendInstruction("Analyze these log entries:");
        pb.AppendInstruction(string.Empty);

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

        pb.AppendUserTextBlock("Log entries", sb.ToString());

        return pb.Build();
    }

    internal static LogAnalysisReport ParseReport(string content, int totalEntries)
    {
        try
        {
            string json = LlmResponseHelper.StripMarkdownCodeFences(content);
            LlmAnalysisResponse? parsed = JsonSerializer.Deserialize<LlmAnalysisResponse>(json, JsonOptions);

            if (parsed is null)
            {
                return new LogAnalysisReport(
                    "AI analysis returned an unparseable response.",
                    [],
                    totalEntries);
            }

            List<LogInsight> insights = parsed.Insights
                ?.Select(i => new LogInsight(
                    i.Description ?? "Unknown",
                    i.Severity ?? "Medium",
                    i.Category ?? "Pattern"))
                .ToList() ?? [];

            return new LogAnalysisReport(
                parsed.Summary ?? "No summary available.",
                insights,
                totalEntries);
        }
        catch (JsonException)
        {
            // Return a generic message instead of raw LLM content to prevent data leakage.
            return new LogAnalysisReport(
                "AI analysis returned a non-JSON response.",
                [],
                totalEntries);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyzing {TruncatedCount} log entries (total: {TotalCount})")]
    private static partial void LogAnalyzingEntries(ILogger logger, int truncatedCount, int totalCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Log analysis complete: {InsightCount} insights from {EntryCount} entries")]
    private static partial void LogAnalysisComplete(ILogger logger, int insightCount, int entryCount);

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    internal sealed class LlmAnalysisResponse
    {
        public string? Summary { get; set; }
        public List<LlmInsightResponse>? Insights { get; set; }
    }

    /// <summary>
    /// Internal DTO for deserializing an individual insight from the LLM response.
    /// </summary>
    internal sealed class LlmInsightResponse
    {
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public string? Category { get; set; }
    }
}
