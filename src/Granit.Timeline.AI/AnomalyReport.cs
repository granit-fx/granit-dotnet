namespace Granit.Timeline.AI;

/// <summary>
/// Result of AI-based timeline anomaly detection.
/// </summary>
/// <param name="HasAnomalies">Whether any anomalies were detected.</param>
/// <param name="Anomalies">List of detected anomalies (empty when <paramref name="HasAnomalies"/> is <c>false</c>).</param>
public sealed record AnomalyReport(bool HasAnomalies, IReadOnlyList<TimelineAnomaly> Anomalies);
