using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Endpoints.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Validates timestamped HMAC-SHA256 signatures on incoming identity webhook payloads.
/// </summary>
/// <remarks>
/// <para>
/// Header format (Stripe-style, also used by <c>Granit.Webhooks</c>):
/// <c>t=&lt;unix-seconds&gt;,v1=&lt;hex&gt;</c>. The HMAC is computed over
/// <c>"&lt;unix-seconds&gt;.&lt;body&gt;"</c>. Including the timestamp inside the
/// signed payload prevents replay attacks: a captured request can only be replayed
/// for the duration of <see cref="IdentityWebhookOptions.ReplayWindow"/> (default
/// 5 minutes), after which the receiver's clock check rejects it.
/// </para>
/// <para>
/// Fail-closed: when no secret is configured, every request is rejected.
/// Configure <see cref="IdentityWebhookOptions.Secret"/> to enable the endpoint.
/// </para>
/// </remarks>
internal sealed partial class WebhookSignatureValidator
{
    private readonly IdentityWebhookOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookSignatureValidator> _logger;

    /// <summary>
    /// Returns <c>true</c> if signature validation is configured (secret is non-empty).
    /// </summary>
    public bool IsEnabled => _options.Secret.Length > 0;

    public WebhookSignatureValidator(
        IOptions<IdentityWebhookOptions> options,
        TimeProvider timeProvider,
        ILogger<WebhookSignatureValidator> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;

        if (!IsEnabled)
        {
            LogWebhookSecretNotConfigured(logger);
        }
    }

    /// <summary>
    /// Validates the timestamped HMAC-SHA256 signature of the given payload.
    /// </summary>
    /// <param name="payload">The raw request body bytes.</param>
    /// <param name="signature">
    /// The signature header value in the format <c>t=&lt;unix-seconds&gt;,v1=&lt;hex&gt;</c>.
    /// </param>
    /// <returns><c>true</c> if valid, <c>false</c> otherwise.</returns>
    public bool Validate(byte[] payload, string? signature)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        if (!TryParseHeader(signature, out long timestamp, out string? expectedHex)
            || expectedHex is null)
        {
            LogMalformedSignatureHeader();
            return false;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        TimeSpan skew = signedAt > now ? signedAt - now : now - signedAt;

        if (skew > _options.ReplayWindow)
        {
            LogSignatureOutsideReplayWindow((long)skew.TotalSeconds, (long)_options.ReplayWindow.TotalSeconds);
            return false;
        }

        // HMAC over "<unix-seconds>.<body>" — same shape as Granit.Webhooks senders.
        byte[] key = Encoding.UTF8.GetBytes(_options.Secret);
        byte[] prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");

        byte[] toSign = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, toSign, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, toSign, prefix.Length, payload.Length);

        byte[] expectedHash = HMACSHA256.HashData(key, toSign);
        string actualHex = Convert.ToHexStringLower(expectedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHex),
            Encoding.UTF8.GetBytes(expectedHex));
    }

    /// <summary>
    /// Parses a <c>t=&lt;unix&gt;,v1=&lt;hex&gt;</c> header. Order of components is not
    /// enforced — callers may send <c>v1=…,t=…</c> as well, matching Stripe's tolerance.
    /// </summary>
    private static bool TryParseHeader(string header, out long timestamp, out string? hex)
    {
        timestamp = 0;
        hex = null;

        foreach (string segment in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = segment.IndexOf('=');
            if (eq <= 0 || eq == segment.Length - 1)
            {
                return false;
            }

            string name = segment[..eq];
            string value = segment[(eq + 1)..];

            if (name.Equals("t", StringComparison.Ordinal))
            {
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                {
                    return false;
                }
            }
            else if (name.Equals("v1", StringComparison.Ordinal))
            {
                hex = value;
            }
        }

        return timestamp > 0 && !string.IsNullOrEmpty(hex);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Identity webhook secret is not configured — all webhook requests will be rejected. " +
                  "Set 'IdentityWebhook:Secret' to enable the webhook endpoint")]
    private static partial void LogWebhookSecretNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Identity webhook signature header is malformed (expected 't=<unix>,v1=<hex>')")]
    private partial void LogMalformedSignatureHeader();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Identity webhook signature timestamp is outside the replay window (skew={SkewSeconds}s, window={WindowSeconds}s)")]
    private partial void LogSignatureOutsideReplayWindow(long skewSeconds, long windowSeconds);
}
