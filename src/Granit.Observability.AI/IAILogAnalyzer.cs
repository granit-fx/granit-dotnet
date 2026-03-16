namespace Granit.Observability.AI;

/// <summary>
/// Analyzes a batch of log entries using AI to produce insights and detect anomalies.
/// </summary>
/// <remarks>
/// The analyzer sends structured log data to an LLM via <see cref="AI.IAIChatClientFactory"/>
/// and parses the response into a structured <see cref="LogAnalysisReport"/>.
/// The workspace used for analysis is configurable via
/// <see cref="Options.ObservabilityAIOptions.WorkspaceName"/>.
/// </remarks>
public interface IAILogAnalyzer
{
    /// <summary>
    /// Analyzes the provided log entries and returns a structured report.
    /// </summary>
    /// <param name="entries">Log entries to analyze. Entries beyond
    /// <see cref="Options.ObservabilityAIOptions.MaxLogEntries"/> are truncated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A report containing a summary and individual insights.</returns>
    Task<LogAnalysisReport> AnalyzeAsync(
        IReadOnlyList<LogEntry> entries,
        CancellationToken cancellationToken = default);
}
