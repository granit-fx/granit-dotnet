using System.Text.RegularExpressions;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates Indian Financial System Codes (IFSC).
/// </summary>
/// <remarks>
/// Eleven characters: 4 letters (bank code) + <c>0</c> (reserved) + 6 alphanumerics (branch code).
/// </remarks>
internal static partial class IfscAlgorithm
{
    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.None, 100)]
    private static partial Regex IfscRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid IFSC code.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IfscRegex().IsMatch(value.Trim().ToUpperInvariant());
    }
}
