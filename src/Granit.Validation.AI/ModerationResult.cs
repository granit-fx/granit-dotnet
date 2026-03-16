namespace Granit.Validation.AI;

/// <summary>
/// Result of an AI content moderation analysis.
/// </summary>
public sealed record ModerationResult
{
    /// <summary>
    /// Whether the analyzed content is acceptable according to the moderation policy.
    /// </summary>
    public required bool IsAcceptable { get; init; }

    /// <summary>
    /// Content policy flags identified by the moderator, filtered by severity threshold.
    /// </summary>
    public required IReadOnlyList<ModerationFlag> Flags { get; init; }
}

/// <summary>
/// A single content policy flag with its category, description, and severity score.
/// </summary>
/// <param name="Category">The moderation category of the flagged content.</param>
/// <param name="Description">Human-readable description of the policy violation.</param>
/// <param name="Severity">Severity score between 0.0 (benign) and 1.0 (severe).</param>
public sealed record ModerationFlag(ModerationCategory Category, string Description, double Severity);

/// <summary>
/// Categories of content policy violations detected by the AI moderator.
/// </summary>
public enum ModerationCategory
{
    /// <summary>Toxic or abusive language.</summary>
    Toxic,

    /// <summary>Harassment or bullying content.</summary>
    Harassment,

    /// <summary>Prompt injection or jailbreak attempts targeting the AI system.</summary>
    PromptInjection,

    /// <summary>Spam, gibberish, or nonsensical content.</summary>
    Spam,

    /// <summary>Violent or threatening content.</summary>
    Violence,

    /// <summary>Content related to self-harm or suicide.</summary>
    SelfHarm,

    /// <summary>Sexually explicit content.</summary>
    Sexual,

    /// <summary>Other policy violations not covered by specific categories.</summary>
    Other,
}
