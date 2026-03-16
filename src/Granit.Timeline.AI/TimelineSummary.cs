namespace Granit.Timeline.AI;

/// <summary>
/// Result of AI-based timeline summarization.
/// </summary>
/// <param name="Text">Natural language summary of the timeline activity.</param>
/// <param name="EntryCount">Number of timeline entries that were analyzed.</param>
/// <param name="OldestEntry">Timestamp of the oldest entry included in the summary, or <c>null</c> if no entries were found.</param>
/// <param name="NewestEntry">Timestamp of the newest entry included in the summary, or <c>null</c> if no entries were found.</param>
public sealed record TimelineSummary(
    string Text,
    int EntryCount,
    DateTimeOffset? OldestEntry,
    DateTimeOffset? NewestEntry);
