using Granit.Validation.Internal;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates SEPA Creditor Identifiers (SCI) per the EPC262-08 specification.
/// </summary>
/// <remarks>
/// Format: country code (2 alpha) + check digits (2 numeric) + creditor business code (3 alphanumeric)
/// + national identifier (variable, up to 28 characters). Total maximum: 35 characters.
/// <para>
/// The check digits are validated using ISO 7064 MOD 97-10 (same algorithm as IBAN):
/// move the first 4 characters to the end, replace each letter with its numeric value
/// (A = 10, …, Z = 35), compute mod 97 — the result must equal 1.
/// </para>
/// </remarks>
internal static class SepaCreditorIdentifierAlgorithm
{
    private const int MinLength = 8;  // CC + 2 + CCC + at least 1 national char
    private const int MaxLength = 35;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid SEPA Creditor Identifier.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Strip spaces.
        string normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal)
                                 .ToUpperInvariant();

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            return false;
        }

        // First 2 characters must be alpha (country code).
        if (!char.IsLetter(normalized[0]) || !char.IsLetter(normalized[1]))
        {
            return false;
        }

        // Characters 3–4 must be digits (check digits).
        if (!char.IsDigit(normalized[2]) || !char.IsDigit(normalized[3]))
        {
            return false;
        }

        // ISO 7064 MOD 97-10: move first 4 characters to end, convert to numeric, compute mod 97.
        string rearranged = normalized[4..] + normalized[..4];
        string numeric = Mod97Algorithm.LettersToDigits(rearranged);
        return Mod97Algorithm.Compute(numeric) == 1;
    }
}
