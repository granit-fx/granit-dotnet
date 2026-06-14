using System.Text;

namespace Granit.Validation.Finance.Internal;

/// <summary>
/// Validates International Bank Account Numbers (IBAN) per ISO 13616.
/// </summary>
/// <remarks>
/// Algorithm:
/// <list type="number">
///   <item>Move the first 4 characters to the end of the string.</item>
///   <item>Replace each letter with two digits: A=10, B=11, …, Z=35.</item>
///   <item>Interpret the resulting string as a decimal integer and compute mod 97.</item>
///   <item>The IBAN is valid if the remainder equals 1.</item>
/// </list>
/// Accepts IBANs with or without spaces (e.g. <c>BE68 5390 0754 7034</c> and <c>BE68539007547034</c>).
/// </remarks>
internal static class IbanAlgorithm
{
    private const int MinLength = 15;
    private const int MaxLength = 34;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid IBAN.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Replace(" ", string.Empty).ToUpperInvariant();

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            return false;
        }

        if (!normalized.All(char.IsLetterOrDigit))
        {
            return false;
        }

        // Move first 4 chars to end, then convert letters to digits
        string rearranged = normalized[4..] + normalized[..4];
        StringBuilder numericString = new(rearranged.Length * 2);

        foreach (char c in rearranged)
        {
            if (char.IsLetter(c))
            {
                numericString.Append(c - 'A' + 10);
            }
            else
            {
                numericString.Append(c);
            }
        }

        return Mod97(numericString.ToString()) == 1;
    }

    private static int Mod97(string numericString)
    {
        int remainder = 0;

        foreach (char c in numericString)
        {
            remainder = (remainder * 10 + (c - '0')) % 97;
        }

        return remainder;
    }
}
