namespace Granit.Observability.AI;

/// <summary>
/// An individual insight discovered by AI analysis of log entries.
/// </summary>
/// <param name="Description">Human-readable description of the insight.</param>
/// <param name="Severity">Severity level (e.g. "Critical", "High", "Medium", "Low").</param>
/// <param name="Category">Classification category (e.g. "Anomaly", "Pattern", "Regression", "Performance").</param>
public sealed record LogInsight(
    string Description,
    string Severity,
    string Category);
