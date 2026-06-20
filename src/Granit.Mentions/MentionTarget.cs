namespace Granit.Mentions;

/// <summary>
/// The resolved representation of a single mention: a human <see cref="Label"/> and the entity
/// <see cref="Content"/>. Content is consumer-defined detail — AI chat injects it into the turn as
/// untrusted data (wrapped in an instruction-isolation envelope before it reaches the model); other
/// consumers may render or ignore it.
/// </summary>
public sealed record MentionTarget
{
    /// <summary>The resolved mention's type.</summary>
    public required string Type { get; init; }

    /// <summary>The resolved mention's identifier.</summary>
    public required string Id { get; init; }

    /// <summary>A concise human label for the entity, e.g. <c>Invoice #42</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The entity detail to hand the consumer (for AI chat, the per-turn context).</summary>
    public required string Content { get; init; }
}
