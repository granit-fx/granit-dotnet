using System.Text.RegularExpressions;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates BIC/SWIFT codes per ISO 9362.
/// </summary>
/// <remarks>
/// Accepts 8-character (primary office) and 11-character (branch) BIC codes:
/// <list type="bullet">
///   <item>4 uppercase alpha — bank code</item>
///   <item>2 uppercase alpha — ISO 3166-1 alpha-2 country code</item>
///   <item>2 alphanumeric — location code</item>
///   <item>3 alphanumeric — branch code (omitted or <c>XXX</c> for primary office)</item>
/// </list>
/// </remarks>
internal static partial class BicSwiftAlgorithm
{
    [GeneratedRegex(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.None, 100)]
    private static partial Regex BicRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid BIC/SWIFT code.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return BicRegex().IsMatch(value.Trim().ToUpperInvariant());
    }
}
