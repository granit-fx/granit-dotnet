namespace Granit.AI.Chat.Mentions;

/// <summary>
/// A single candidate an <see cref="IAIMentionResolver"/> surfaces for the <c>@</c> picker as the
/// user types (ADR-067). It carries only what the front needs to render a row and, on selection,
/// build an <see cref="AIMention"/> — never the entity body, which is fetched at send time via
/// <see cref="IAIMentionResolver.ResolveAsync"/> under the caller's ACLs.
/// </summary>
/// <remarks>
/// Search runs per scope under the caller's identity and ACLs, exactly like resolution: a resolver
/// MUST surface only entities the caller may see. A type the caller cannot search returns an empty
/// list, never an error.
/// </remarks>
public sealed record AIMentionSuggestion
{
    /// <summary>The mention type, echoing the producing <see cref="IAIMentionResolver.Type"/>.</summary>
    public required string Type { get; init; }

    /// <summary>The opaque entity identifier carried back on the resulting <see cref="AIMention.Id"/>.</summary>
    public required string Id { get; init; }

    /// <summary>A concise human label for the row, e.g. <c>Ada Lovelace</c> or <c>Invoice #42</c>.</summary>
    public required string Label { get; init; }

    /// <summary>An optional secondary line, e.g. an email or status, or <see langword="null"/>.</summary>
    public string? Description { get; init; }
}
