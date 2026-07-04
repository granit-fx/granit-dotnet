namespace Granit.Validation.Europe.Internal;

/// <summary>
/// Validates French RIB (Relevé d'Identité Bancaire) bank account numbers.
/// </summary>
/// <remarks>
/// A French RIB is exactly 23 characters:
/// <list type="bullet">
///   <item>Characters 1–5 : bank code (code banque) — 5 digits</item>
///   <item>Characters 6–10 : branch code (code guichet) — 5 digits</item>
///   <item>Characters 11–21 : account number (numéro de compte) — 11 alphanumeric characters</item>
///   <item>Characters 22–23 : check key (clé de contrôle) — 2 digits (01–97)</item>
/// </list>
/// Letters in the account number are substituted before computation:
/// A,J→1 ; B,K,S→2 ; C,L,T→3 ; D,M,U→4 ; E,N,V→5 ;
/// F,O,W→6 ; G,P,X→7 ; H,Q,Y→8 ; I,R,Z→9.
/// <para>
/// Key formula: <c>clé = 97 − (89 × banque + 15 × guichet + 3 × compte) mod 97</c>.
/// When the modulus is zero the key is 97.
/// </para>
/// Spaces and dashes are stripped before validation.
/// </remarks>
internal static class FrenchRibAlgorithm
{
    private const int RibLength = 23;

    // Maps A–Z (index 0–25) to RIB digit equivalents per the official specification.
    private static readonly int[] LetterValues =
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, // A–I → 1–9
        1, 2, 3, 4, 5, 6, 7, 8, 9, // J–R → 1–9
        2, 3, 4, 5, 6, 7, 8, 9,    // S–Z → 2–9
    ];

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is a valid French RIB.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = Normalize(value);

        if (normalized.Length != RibLength)
        {
            return false;
        }

        string bankStr = normalized[..5];
        string branchStr = normalized[5..10];
        string accountStr = normalized[10..21];
        string keyStr = normalized[21..];

        if (!IsAllDigits(bankStr) || !IsAllDigits(branchStr) || !IsAllDigits(keyStr))
        {
            return false;
        }

        string accountDigits = ConvertAccountLetters(accountStr);

        if (accountDigits.Length != 11 || !IsAllDigits(accountDigits))
        {
            return false;
        }

        long bank = long.Parse(bankStr);
        long branch = long.Parse(branchStr);
        long account = long.Parse(accountDigits);
        int providedKey = int.Parse(keyStr);

        long remainder = ((89L * bank) + (15L * branch) + (3L * account)) % 97;
        int expectedKey = remainder == 0 ? 97 : (int)(97 - remainder);

        return expectedKey == providedKey;
    }

    private static string Normalize(string value)
    {
        System.Text.StringBuilder sb = new(RibLength);
        foreach (char c in value.Where(c => c != ' ' && c != '-'))
        {
            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    private static bool IsAllDigits(string s) => s.All(c => c >= '0' && c <= '9');

    private static string ConvertAccountLetters(string account)
    {
        System.Text.StringBuilder sb = new(account.Length);
        foreach (char c in account)
        {
            if (c >= '0' && c <= '9')
            {
                sb.Append(c);
            }
            else if (c >= 'A' && c <= 'Z')
            {
                sb.Append(LetterValues[c - 'A']);
            }
            else
            {
                // Invalid character — signal failure via empty string.
                return string.Empty;
            }
        }

        return sb.ToString();
    }
}
