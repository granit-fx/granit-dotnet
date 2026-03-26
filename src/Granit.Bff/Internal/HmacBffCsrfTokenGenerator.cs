using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Internal;

/// <summary>
/// HMAC-SHA256 based CSRF token generator.
/// Token format: <c>{timestampUnixSeconds}:{hmac-hex}</c>.
/// Validation checks both HMAC integrity and a 24-hour sliding window.
/// </summary>
/// <remarks>
/// The HMAC key is resolved from <see cref="GranitBffOptions.CsrfHmacKey"/> (base64-encoded).
/// When not configured, a random key is generated at startup — suitable for single-instance
/// deployments only. Multi-instance deployments MUST configure a shared key.
/// </remarks>
internal sealed partial class HmacBffCsrfTokenGenerator(
    IClock clock,
    IOptions<GranitBffOptions> options,
    ILogger<HmacBffCsrfTokenGenerator> logger) : IBffCsrfTokenGenerator
{
    private static readonly TimeSpan ValidationWindow = TimeSpan.FromHours(24);
    private readonly byte[] _key = ResolveKey(options.Value, logger);

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

    private static byte[] ResolveKey(GranitBffOptions bffOptions, ILogger logger)
    {
        if (!string.IsNullOrEmpty(bffOptions.CsrfHmacKey))
        {
            return Convert.FromBase64String(bffOptions.CsrfHmacKey);
        }

        LogCsrfKeyGenerated(logger);
        return RandomNumberGenerator.GetBytes(32);
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BFF CSRF: no CsrfHmacKey configured — generated random key. "
            + "CSRF tokens will be invalidated on restart and are not shared across instances")]
    private static partial void LogCsrfKeyGenerated(ILogger logger);
}
