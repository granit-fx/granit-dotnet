namespace Granit.Timeline.AI;

/// <summary>
/// A single anomaly detected in a timeline stream.
/// </summary>
/// <param name="Description">Human-readable description of the anomaly.</param>
/// <param name="Severity">Severity level: <c>Low</c>, <c>Medium</c>, or <c>High</c>.</param>
public sealed record TimelineAnomaly(string Description, string Severity);
