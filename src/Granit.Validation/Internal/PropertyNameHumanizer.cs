using System.Globalization;
using System.Text.RegularExpressions;

namespace Granit.Validation.Internal;

/// <summary>
/// Humanizes PascalCase / camelCase member names into space-separated, sentence-cased
/// labels for use as the <c>{PropertyName}</c> placeholder in validation messages
/// (e.g. <c>NewPassword</c> → <c>New password</c>, <c>ConfirmEmailAddress</c> →
/// <c>Confirm email address</c>).
/// </summary>
/// <remarks>
/// Wired globally via <c>ValidatorOptions.Global.DisplayNameResolver</c> so every
/// validated property reads naturally without per-rule <c>WithName(...)</c> calls.
/// Acronym runs (e.g. <c>ID</c>, <c>URL</c>) are kept intact.
/// </remarks>
internal static partial class PropertyNameHumanizer
{
    /// <summary>
    /// Inserts a boundary before an uppercase letter that follows a lowercase letter or
    /// digit (<c>newPassword</c> → <c>new|Password</c>), and before an uppercase letter
    /// that begins a new word after an acronym run (<c>HTTPServer</c> → <c>HTTP|Server</c>).
    /// </summary>
    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex BoundaryRegex();

    /// <summary>
    /// Humanizes a member name. Returns the input unchanged when it is null, empty,
    /// or already contains a space (already a friendly name).
    /// </summary>
    public static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Contains(' ', StringComparison.Ordinal))
        {
            return name;
        }

        string spaced = BoundaryRegex().Replace(name, " ");
        string[] words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            return name;
        }

        for (int i = 1; i < words.Length; i++)
        {
            // Lowercase ordinary words, but keep acronym runs (all-uppercase) as-is.
            if (!IsAcronym(words[i]))
            {
                words[i] = words[i].ToLower(CultureInfo.CurrentCulture);
            }
        }

        return string.Join(' ', words);
    }

    private static bool IsAcronym(string word)
    {
        foreach (char c in word)
        {
            if (char.IsLower(c))
            {
                return false;
            }
        }

        return true;
    }
}
