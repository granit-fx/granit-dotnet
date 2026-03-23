using System.Text.RegularExpressions;

namespace Granit.Timeline.Internal;

/// <summary>
/// Extracts mentioned user IDs from Markdown body.
/// Format: <c>@[Display Name](user:550e8400-e29b-41d4-a716-446655440000)</c>.
/// </summary>
internal static partial class MentionParser
{
    [GeneratedRegex(@"@\[[^\]]+\]\(user:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex MentionPattern();

    /// <summary>
    /// Extracts distinct mentioned user IDs from a Markdown body.
    /// Only well-formed GUIDs in the <c>@[Name](user:guid)</c> format are extracted.
    /// </summary>
    public static IReadOnlyList<string> ExtractMentionedUserIds(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        MatchCollection matches = MentionPattern().Matches(body);
        if (matches.Count == 0)
        {
            return [];
        }

        HashSet<string> userIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
        {
            userIds.Add(match.Groups[1].Value);
        }

        return [.. userIds];
    }
}
