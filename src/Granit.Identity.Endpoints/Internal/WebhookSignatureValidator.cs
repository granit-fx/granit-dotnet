using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Endpoints.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Validates HMAC-SHA256 signatures on incoming webhook payloads.
/// </summary>
/// <remarks>
/// Fail-closed: when no secret is configured, all requests are rejected.
/// Configure <see cref="IdentityWebhookOptions.Secret"/> to enable the webhook endpoint.
/// </remarks>
internal sealed partial class WebhookSignatureValidator
{
    private readonly IdentityWebhookOptions _options;

    /// <summary>
    /// Returns <c>true</c> if signature validation is configured (secret is non-empty).
    /// </summary>
    public bool IsEnabled => _options.Secret.Length > 0;

    public WebhookSignatureValidator(
        IOptions<IdentityWebhookOptions> options,
        ILogger<WebhookSignatureValidator> logger)
    {
        _options = options.Value;

        if (!IsEnabled)
        {
            LogWebhookSecretNotConfigured(logger);
        }
    }

    /// <summary>
    /// Validates the HMAC-SHA256 signature of the given payload.
    /// </summary>
    /// <param name="payload">The raw request body bytes.</param>
    /// <param name="signature">The signature from the HTTP header (hex-encoded).</param>
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

        byte[] key = Encoding.UTF8.GetBytes(_options.Secret);
        byte[] expectedHash = HMACSHA256.HashData(key, payload);
        string expectedSignature = Convert.ToHexStringLower(expectedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Identity webhook secret is not configured — all webhook requests will be rejected. " +
                  "Set 'IdentityWebhook:Secret' to enable the webhook endpoint")]
    private static partial void LogWebhookSecretNotConfigured(ILogger logger);
}
