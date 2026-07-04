namespace Granit.Validation.Europe.Internal;

/// <summary>
/// Validates French VAT numbers (numéro de TVA intracommunautaire).
/// </summary>
/// <remarks>
/// Format: <c>FR</c> + 2-digit numeric key + 9-digit SIREN = 13 characters.
/// The key is computed as: <c>(12 + 3 × (SIREN mod 97)) mod 97</c>, zero-padded to 2 digits.
/// Alpha keys (introduced in 2013 for some issuers) are not covered by this validator.
/// </remarks>
internal static class FrenchVatAlgorithm
{
    private const int TotalLength = 13; // FR(2) + key(2) + SIREN(9)

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid French VAT number.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length != TotalLength)
        {
            return false;
        }

        if (!normalized.StartsWith("FR", StringComparison.Ordinal))
        {
            return false;
        }

        string keyPart = normalized[2..4];
        string sirenPart = normalized[4..];

        // Key must be 2 digits.
        if (!keyPart.All(char.IsDigit))
        {
            return false;
        }

        // SIREN must be exactly 9 digits.
        if (!sirenPart.All(char.IsDigit))
        {
            return false;
        }

        if (!long.TryParse(sirenPart, out long siren))
        {
            return false;
        }

        if (!int.TryParse(keyPart, out int providedKey))
        {
            return false;
        }

        int computedKey = (int)((12 + (3 * (siren % 97))) % 97);
        return computedKey == providedKey;
    }
}
