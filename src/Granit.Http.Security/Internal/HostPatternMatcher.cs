using System.Globalization;

namespace Granit.Http.Security.Internal;

/// <summary>
/// Matches hostnames against simple shell-like patterns.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>*</c> alone matches any host.</item>
///   <item>Patterns starting with <c>*.</c> match any subdomain AND the apex
///   (e.g. <c>*.example.com</c> matches both <c>api.example.com</c> and <c>example.com</c>).</item>
///   <item>Otherwise the comparison is an exact, case-insensitive match.</item>
/// </list>
/// Both host and pattern are normalized to ASCII via <see cref="IdnMapping"/> to handle
/// internationalized domain names consistently.
/// </remarks>
internal static class HostPatternMatcher
{
    private static readonly IdnMapping IdnMapping = new();

    public static bool Matches(string host, string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        if (pattern == "*")
        {
            return true;
        }

        string normalizedHost = NormalizeAscii(host);
        string normalizedPattern = pattern.StartsWith("*.", StringComparison.Ordinal)
            ? "*." + NormalizeAscii(pattern[2..])
            : NormalizeAscii(pattern);

        if (normalizedPattern.StartsWith("*.", StringComparison.Ordinal))
        {
            string rest = normalizedPattern[2..];
            if (string.Equals(normalizedHost, rest, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return normalizedHost.EndsWith("." + rest, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(normalizedHost, normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesAny(string host, IReadOnlyList<string> patterns)
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            if (Matches(host, patterns[i]))
            {
                return true;
            }
        }
        return false;
    }

    private static string NormalizeAscii(string value)
    {
        try
        {
            return IdnMapping.GetAscii(value);
        }
        catch (ArgumentException)
        {
            // Already ASCII or contains characters IdnMapping rejects (e.g. underscores in tests).
            return value;
        }
    }
}
