using System.Text.RegularExpressions;

namespace Granit.Validation.Europe.Internal.Italy;

/// <summary>
/// Validates Italian postal codes (CAP — Codice di Avviamento Postale).
/// </summary>
/// <remarks>
/// Format: exactly 5 digits. The first two digits represent the province code (00–98).
/// Valid range: 00010–98168.
/// </remarks>
internal static partial class ItalianPostalCodeAlgorithm
{
    [GeneratedRegex(@"^\d{5}$", RegexOptions.None, 100)]
    private static partial Regex CapRegex();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid Italian CAP.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        if (!CapRegex().IsMatch(trimmed))
        {
            return false;
        }

        // First two digits must be in range 00–98.
        int prefix = ((trimmed[0] - '0') * 10) + (trimmed[1] - '0');
        return prefix <= 98;
    }
}
