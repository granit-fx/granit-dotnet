namespace Granit.AI.Chat.Clarification;

/// <summary>
/// A typed clarification the agent surfaces when it needs disambiguation (ADR-067): a
/// <see cref="Question"/> plus discrete <see cref="Options"/> the front renders as clickable
/// choices, optionally with an "Other (describe)" free-text affordance. Unlike a suggested action
/// (#2642), a clarification <strong>blocks the turn</strong> — the loop resumes only when the user
/// answers (their choice becomes the next user turn). Single-select in v1.
/// </summary>
public sealed record AIClarificationRequest
{
    /// <summary>The interrupt kind that carries a clarification on the orchestration result.</summary>
    public const string InterruptKind = "clarification";

    /// <summary>The disambiguating question to show the user.</summary>
    public required string Question { get; init; }

    /// <summary>The discrete choices, each rendered as a button.</summary>
    public required IReadOnlyList<AIClarificationOption> Options { get; init; }

    /// <summary>Whether to offer an "Other (describe)" free-text affordance alongside the options.</summary>
    public bool AllowOther { get; init; }
}

/// <summary>One clickable choice of an <see cref="AIClarificationRequest"/>.</summary>
public sealed record AIClarificationOption
{
    /// <summary>The display label for the choice.</summary>
    public required string Label { get; init; }

    /// <summary>
    /// The value sent back as the next turn when chosen. Falls back to <see cref="Label"/> when omitted.
    /// </summary>
    public string? Value { get; init; }
}
