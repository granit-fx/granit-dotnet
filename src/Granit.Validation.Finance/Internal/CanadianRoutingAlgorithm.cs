using System.Text.RegularExpressions;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates Canadian routing numbers (5-digit transit/branch + 3-digit financial institution).
/// </summary>
/// <remarks>
/// Eight digits, optionally written as <c>XXXXX-YYY</c> (transit-institution). No published check
/// digit. The 9-digit electronic MICR form (<c>0YYYXXXXX</c>) is also accepted.
/// </remarks>
internal static partial class CanadianRoutingAlgorithm
{
    [GeneratedRegex("^[0-9]{8}$", RegexOptions.None, 100)]
    private static partial Regex PaperFormRegex();

    [GeneratedRegex("^0[0-9]{8}$", RegexOptions.None, 100)]
    private static partial Regex ElectronicFormRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid Canadian routing number.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return PaperFormRegex().IsMatch(normalized) || ElectronicFormRegex().IsMatch(normalized);
    }
}
