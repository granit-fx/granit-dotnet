namespace Granit.AI.Chat.Mentions;

/// <summary>
/// The context an <see cref="IAIMentionResolver"/> produced for a single resolved mention:
/// a short human <see cref="Label"/> and the entity <see cref="Content"/> to inject into the
/// turn. The content is treated as untrusted data — the framework wraps it in the
/// instruction-isolation envelope before it reaches the model.
/// </summary>
public sealed record AIMentionContext
{
    /// <summary>The resolved mention's type (echoes <see cref="AIMention.Type"/>).</summary>
    public required string Type { get; init; }

    /// <summary>The resolved mention's identifier (echoes <see cref="AIMention.Id"/>).</summary>
    public required string Id { get; init; }

    /// <summary>A concise human label for the entity, e.g. <c>Invoice #42</c>.</summary>
    public required string Label { get; init; }

    /// <summary>The entity data to hand the agent as context for this turn.</summary>
    public required string Content { get; init; }
}
