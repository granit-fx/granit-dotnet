using System.Text.RegularExpressions;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates US ABA routing transit numbers (RTN).
/// </summary>
/// <remarks>
/// Nine digits validated with the ABA mod-10 checksum
/// <c>3·(d1+d4+d7) + 7·(d2+d5+d8) + 1·(d3+d6+d9) ≡ 0 (mod 10)</c>.
/// </remarks>
internal static partial class AbaRoutingAlgorithm
{
    [GeneratedRegex("^[0-9]{9}$", RegexOptions.None, 100)]
    private static partial Regex AbaRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid ABA routing number.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (!AbaRegex().IsMatch(normalized))
        {
            return false;
        }

        ReadOnlySpan<int> weights = [3, 7, 1, 3, 7, 1, 3, 7, 1];
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            sum += (normalized[i] - '0') * weights[i];
        }

        return sum % 10 == 0;
    }
}
