using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Timing;

namespace Granit.Bff.Internal;

/// <summary>
/// HMAC-SHA256 based CSRF token generator. Uses a random key generated at startup.
/// Token format: <c>{timestampUnixSeconds}:{hmac-hex}</c>.
/// Validation checks both HMAC integrity and a 24-hour sliding window.
/// </summary>
internal sealed class HmacBffCsrfTokenGenerator(IClock clock) : IBffCsrfTokenGenerator
{
    private static readonly TimeSpan ValidationWindow = TimeSpan.FromHours(24);
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public string Generate(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        long timestamp = clock.Now.ToUnixTimeSeconds();
        string hmac = ComputeHmac(sessionId, timestamp);

        return $"{timestamp}:{hmac}";
    }

    public bool Validate(string sessionId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string[] parts = token.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], CultureInfo.InvariantCulture, out long timestamp))
        {
            return false;
        }

        // Check timestamp is within the validation window
        long nowSeconds = clock.Now.ToUnixTimeSeconds();
        if (Math.Abs(nowSeconds - timestamp) > (long)ValidationWindow.TotalSeconds)
        {
            return false;
        }

        string expectedHmac = ComputeHmac(sessionId, timestamp);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(parts[1]),
            Encoding.UTF8.GetBytes(expectedHmac));
    }

    private string ComputeHmac(string sessionId, long timestamp)
    {
        string payload = $"{sessionId}:{timestamp}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = HMACSHA256.HashData(_key, payloadBytes);

        return Convert.ToHexStringLower(hash);
    }
}
