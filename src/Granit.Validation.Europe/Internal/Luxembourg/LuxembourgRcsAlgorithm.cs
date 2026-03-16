using System.Text.RegularExpressions;

namespace Granit.Validation.Europe.Internal.Luxembourg;

/// <summary>
/// Validates Luxembourg RCS numbers (Registre de Commerce et des Sociétés).
/// </summary>
/// <remarks>
/// Format: a single letter prefix (A–J or S) followed by 1 to 6 digits.
/// <list type="bullet">
///   <item>A: public limited company (société anonyme)</item>
///   <item>B: private limited company (société à responsabilité limitée)</item>
///   <item>C: partnership limited by shares (société en commandite par actions)</item>
///   <item>D: limited partnership (société en commandite simple)</item>
///   <item>E: general partnership (société en nom collectif)</item>
///   <item>F: civil company (société civile)</item>
///   <item>G: economic interest grouping (groupement d'intérêt économique)</item>
///   <item>H: European company (société européenne)</item>
///   <item>I: non-profit association (association sans but lucratif)</item>
///   <item>J: cooperative company (société coopérative)</item>
///   <item>S: branch of a foreign company (succursale d'une société étrangère)</item>
/// </list>
/// Spaces are stripped and input is uppercased before validation.
/// </remarks>
internal static partial class LuxembourgRcsAlgorithm
{
    [GeneratedRegex(@"^[A-JS]\d{1,6}$", RegexOptions.None, 100)]
    private static partial Regex RcsRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid Luxembourg RCS number.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal)
                                  .Trim()
                                  .ToUpperInvariant();

        return RcsRegex().IsMatch(normalized);
    }
}
