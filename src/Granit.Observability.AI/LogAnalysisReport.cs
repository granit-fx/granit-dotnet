namespace Granit.Observability.AI;

/// <summary>
/// Report produced by AI analysis of a batch of log entries.
/// </summary>
/// <param name="Summary">High-level summary of the analysis findings.</param>
/// <param name="Insights">Individual insights discovered during analysis.</param>
/// <param name="TotalEntries">Number of log entries that were analyzed.</param>
public sealed record LogAnalysisReport(
    string Summary,
    IReadOnlyList<LogInsight> Insights,
    int TotalEntries);
