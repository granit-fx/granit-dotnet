namespace Granit.Validation.Internal;

/// <summary>
/// ISO 7064 MOD 97-10 check-digit computation shared by IBAN, SEPA Creditor Identifier,
/// Belgian INAMI, Belgian eID, and similar identifiers.
/// </summary>
internal static class Mod97Algorithm
{
    /// <summary>
    /// Computes the MOD-97 remainder of the numeric string <paramref name="numericString"/>.
    /// The string may represent a very large integer; computation is performed digit by digit
    /// to avoid overflow.
    /// </summary>
    public static int Compute(string numericString)
    {
        int remainder = 0;
        foreach (char c in numericString)
        {
            remainder = ((remainder * 10) + (c - '0')) % 97;
        }

        return remainder;
    }

    /// <summary>
    /// Converts an alphanumeric ISO 7064 string (letters and digits) to its all-digit
    /// representation: A = 10, B = 11, …, Z = 35.
    /// </summary>
    public static string LettersToDigits(string value)
    {
        System.Text.StringBuilder sb = new(value.Length * 2);
        foreach (char c in value)
        {
            if (char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c) - 'A' + 10);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
