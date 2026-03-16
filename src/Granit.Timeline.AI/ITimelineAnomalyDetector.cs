namespace Granit.Timeline.AI;

/// <summary>
/// Detects unusual patterns in timeline activity using an LLM.
/// </summary>
/// <remarks>
/// Analyzes activity streams for anomalies such as bulk edits, off-hours activity,
/// privilege escalation, and other suspicious patterns.
/// </remarks>
public interface ITimelineAnomalyDetector
{
    /// <summary>
    /// Analyzes the activity stream for a specific entity and reports detected anomalies.
    /// </summary>
    /// <param name="entityType">The entity type (e.g. "Order", "Ticket").</param>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A report indicating whether anomalies were found and their details.</returns>
    Task<AnomalyReport> DetectAnomaliesAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);
}
