namespace Granit.Timeline.Domain;

/// <summary>
/// Closed catalog of reaction emojis (ADR-046 §1, story C3). Extending the
/// catalog requires an ADR amendment — same pattern as ADR-048 §2 for
/// cross-module relation kinds.
/// </summary>
/// <remarks>
/// <para>
/// The wire identifier is the snake_case key (e.g. <c>"thumbs_up"</c>),
/// not the rendered glyph — keeps the API stable across font / Unicode
/// changes and lets the React shell pick the rendering. Display labels
/// are localized via the <c>Reaction:{key}</c> resource keys in
/// <c>Granit.Timeline.Endpoints</c> (18 cultures).
/// </para>
/// </remarks>
public static class ReactionEmojiCatalog
{
    /// <summary>The 5 v1 emojis: 👍 ❤️ 🎉 😂 👀.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "thumbs_up",
        "heart",
        "tada",
        "joy",
        "eyes",
    ];

    private static readonly HashSet<string> Lookup = new(All, StringComparer.Ordinal);

    /// <summary>Returns <see langword="true"/> when <paramref name="emoji"/> is in the closed catalog.</summary>
    public static bool IsValid(string emoji) =>
        !string.IsNullOrEmpty(emoji) && Lookup.Contains(emoji);
}
