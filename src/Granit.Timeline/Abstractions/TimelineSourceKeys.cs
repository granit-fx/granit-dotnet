using System.Text.RegularExpressions;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Well-known <c>SourceKey</c> values for federated timeline sources.
/// Contributors declare their own key (e.g. <c>"auditing"</c>, <c>"workflow"</c>) — the set
/// is pluggable, not an enum.
/// </summary>
public static partial class TimelineSourceKeys
{
    /// <summary>Reserved key for entries stored directly in the Timeline table.</summary>
    public const string Native = "native";

    /// <summary>
    /// Validates a candidate source key against the convention
    /// <c>^[a-z][a-z0-9_-]{0,63}$</c>. Lower-case so that case-insensitive
    /// PostgreSQL collations don't collide with the partial unique index on
    /// shadow rows.
    /// </summary>
    public static bool IsValid(string sourceKey) =>
        !string.IsNullOrEmpty(sourceKey) && SourceKeyPattern().IsMatch(sourceKey);

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceKeyPattern();
}
