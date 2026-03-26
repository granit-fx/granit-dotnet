using System.Security.Cryptography;
using System.Text;

namespace Granit.Authentication.ApiKeys.Internal;

/// <summary>
/// Generates cryptographically secure API keys with Stripe-style prefixes.
/// </summary>
/// <remarks>
/// Key format: <c>gk_{environment}_{typeCode}_{random}</c> where <c>random</c>
/// is 32 bytes of <see cref="RandomNumberGenerator"/> output encoded as base62.
/// </remarks>
internal sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private const int RandomByteCount = 32;
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <inheritdoc/>
    public ApiKeyGenerationResult Generate(ApiKeyType type, string environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string prefix = $"gk_{environment}_{GetTypeCode(type)}_";
        string random = GenerateRandomBase62(RandomByteCount);
        string rawSecret = $"{prefix}{random}";
        string hashedKey = ComputeSha256(rawSecret);
        string lastFour = rawSecret[^4..];

        return new ApiKeyGenerationResult(rawSecret, hashedKey, prefix, lastFour);
    }

    internal static string ComputeSha256(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    private static string GetTypeCode(ApiKeyType type) => type switch
    {
        ApiKeyType.Secret => "sk",
        ApiKeyType.Publishable => "pk",
        ApiKeyType.Webhook => "wh",
        ApiKeyType.Ephemeral => "ep",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>
    /// Generates a uniform random base62 string using rejection sampling
    /// to eliminate modulo bias (256 mod 62 = 8 biased characters).
    /// </summary>
    private static string GenerateRandomBase62(int length)
    {
        // Rejection threshold: largest multiple of 62 that fits in a byte (62 * 4 = 248)
        const int rejectionThreshold = 248;

        char[] result = new char[length];
        Span<byte> buffer = stackalloc byte[1];

        for (int i = 0; i < length; i++)
        {
            byte value;
            do
            {
                RandomNumberGenerator.Fill(buffer);
                value = buffer[0];
            }
            while (value >= rejectionThreshold);

            result[i] = Base62Chars[value % Base62Chars.Length];
        }

        return new string(result);
    }
}
