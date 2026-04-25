using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Hosting;
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
    IHostEnvironment environment,
    ILogger<HmacBffCsrfTokenGenerator> logger) : IBffCsrfTokenGenerator
{
    // Validation window kept short (1h) to limit replay surface from leaked tokens
    // (referer headers, browser caches, dev tools). Future timestamps are rejected
    // outright with a small clock-skew tolerance.
    private static readonly TimeSpan ValidationWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);
    private readonly byte[] _key = ResolveKey(options.Value, environment, logger);

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

        // Reject expired tokens (older than ValidationWindow) and future-dated tokens
        // beyond clock-skew tolerance — both indicate forgery or significant drift.
        long nowSeconds = clock.Now.ToUnixTimeSeconds();
        long age = nowSeconds - timestamp;
        if (age > (long)ValidationWindow.TotalSeconds || age < -(long)ClockSkew.TotalSeconds)
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

    private static byte[] ResolveKey(GranitBffOptions bffOptions, IHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrEmpty(bffOptions.CsrfHmacKey))
        {
            return Convert.FromBase64String(bffOptions.CsrfHmacKey);
        }

        // Auto-generated keys are per-instance: tokens minted on instance A are
        // rejected by instance B, breaking sessions silently in any multi-replica
        // deployment. Refuse outside Development.
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Bff:CsrfHmacKey is required outside the Development environment "
                + $"(current: '{environment.EnvironmentName}'). Without a shared key, multi-instance "
                + "deployments cannot validate each other's CSRF tokens. Generate one with "
                + "Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) and store it in Vault.");
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
