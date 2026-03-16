namespace Granit.Timeline.AI;

/// <summary>
/// Summarizes a timeline stream into a concise natural language summary using an LLM.
/// </summary>
public interface ITimelineSummarizer
{
    /// <summary>
    /// Generates a natural language summary of the activity stream for a specific entity.
    /// </summary>
    /// <param name="entityType">The entity type (e.g. "Order", "Ticket").</param>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="since">Optional lower bound — only entries after this timestamp are included.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A summary containing the generated text and metadata about analyzed entries.</returns>
    Task<TimelineSummary> SummarizeAsync(
        string entityType,
        Guid entityId,
        DateTimeOffset? since = null,
        CancellationToken ct = default);
}
