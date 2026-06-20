namespace Granit.Mentions;

/// <summary>
/// A single candidate surfaced for the <c>@</c> picker as the user types. Carries only what the
/// front needs to render a row and, on selection, build the typed reference — never the entity body,
/// which is fetched on resolve.
/// </summary>
public sealed record MentionSuggestion
{
    /// <summary>The mention type, echoing the producing <see cref="IMentionResolver.Type"/>.</summary>
    public required string Type { get; init; }

    /// <summary>The opaque entity identifier carried back on the resulting reference.</summary>
    public required string Id { get; init; }

    /// <summary>A concise human label for the row, e.g. <c>Ada Lovelace</c> or <c>Invoice #42</c>.</summary>
    public required string Label { get; init; }

    /// <summary>An optional secondary line, e.g. an email or status, or <see langword="null"/>.</summary>
    public string? Description { get; init; }
}
