using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Options;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Internal;

/// <summary>
/// HMAC-SHA256 <see cref="IUserLookupHasher"/>. Pepper sourced from
/// <see cref="UserLookupHasherOptions.Pepper"/> and validated at first
/// resolution — an unset pepper fails fast so production deployments cannot
/// accidentally run with a known-zero key.
/// </summary>
internal sealed class HmacUserLookupHasher(IOptions<UserLookupHasherOptions> options)
    : IUserLookupHasher
{
    private readonly byte[] _pepper = ResolvePepper(options.Value);

    public string? ComputeEmailHash(string? email) =>
        Compute(NormaliseEmail(email));

    public string? ComputePhoneHash(string? phoneNumber) =>
        Compute(NormalisePhone(phoneNumber));

    private string? Compute(string? normalised)
    {
        if (string.IsNullOrEmpty(normalised))
        {
            return null;
        }

        byte[] payload = Encoding.UTF8.GetBytes(normalised);
        byte[] digest = HMACSHA256.HashData(_pepper, payload);
        return Convert.ToHexStringLower(digest);
    }

    // Same canonicalisation as Granit.Identity.Federated.HmacUserLookupHasher:
    // case-insensitive lookup, leading/trailing whitespace collapsed.
    private static string? NormaliseEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        return email.Trim().ToLowerInvariant();
    }

    // Phones are case-insensitive trivially (digits + plus sign); whitespace
    // and separators collapsed so "+32 470 12 34 56" and "+32470123456"
    // produce the same digest. Separators allowed: space, dash, dot,
    // parenthesis. Anything else passes through (E.164 plus prefix, digits).
    private static string? NormalisePhone(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[phoneNumber.Length];
        int length = 0;
        foreach (char c in phoneNumber)
        {
            if (c is ' ' or '-' or '.' or '(' or ')')
            {
                continue;
            }
            buffer[length++] = c;
        }
        return length == 0 ? null : new string(buffer[..length]);
    }

    private static byte[] ResolvePepper(UserLookupHasherOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Pepper))
        {
            throw new InvalidOperationException(
                "Identity:LookupHasher:Pepper is required when at-rest encryption is enabled " +
                "on Granit.Identity. The pepper must be at least 32 bytes of high-entropy " +
                "random data (e.g. openssl rand -hex 32) and stored separately from the " +
                "encryption key ring so the two can be rotated independently.");
        }

        string pepper = options.Pepper.Trim();
        if (pepper.Length % 2 == 0 && pepper.All(IsHexDigit))
        {
            try
            {
                return Convert.FromHexString(pepper);
            }
            catch (FormatException)
            {
                // fall through to UTF-8 interpretation
            }
        }

        return Encoding.UTF8.GetBytes(pepper);
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
