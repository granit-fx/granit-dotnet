using System.Text.RegularExpressions;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates Australian/New Zealand BSB (Bank-State-Branch) codes.
/// </summary>
/// <remarks>
/// Six digits, optionally written with a hyphen (<c>XXX-XXX</c>). No published check digit.
/// </remarks>
internal static partial class BsbAlgorithm
{
    [GeneratedRegex("^[0-9]{6}$", RegexOptions.None, 100)]
    private static partial Regex BsbRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid BSB code.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return BsbRegex().IsMatch(normalized);
    }
}
