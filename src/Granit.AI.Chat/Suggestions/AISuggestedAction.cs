namespace Granit.AI.Chat.Suggestions;

/// <summary>
/// A typed, non-executing call-to-action the agent surfaces alongside its answer (ADR-067):
/// a label and a deep link the front renders so the user can act on a detected gap (e.g.
/// "connect a calendar"). v1 is purely declarative — there is no execution path; the record
/// carries no handler or callback, only display data and a destination. Execution is phase 2.
/// </summary>
public sealed record AISuggestedAction
{
    /// <summary>The suggestion type, e.g. <c>calendar.connect</c>. Unique per action within a turn.</summary>
    public required string Type { get; init; }

    /// <summary>The display label for the call-to-action.</summary>
    public required string Label { get; init; }

    /// <summary>The deep link the front navigates to (a relative route or URL). Never auto-invoked.</summary>
    public required string DeepLink { get; init; }

    /// <summary>An optional one-line explanation of why the action is suggested.</summary>
    public string? Description { get; init; }
}
