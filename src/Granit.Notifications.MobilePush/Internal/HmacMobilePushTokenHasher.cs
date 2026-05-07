using System.Security.Cryptography;
using System.Text;
using Granit.Notifications.MobilePush.Options;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// HMAC-SHA256 <see cref="IMobilePushTokenHasher"/>. Pepper sourced from
/// <see cref="MobilePushTokenHasherOptions.DeviceTokenLookupPepper"/> and
/// validated at startup — an unset pepper fails fast so production deployments
/// cannot accidentally run with a known-zero key.
/// </summary>
internal sealed class HmacMobilePushTokenHasher(IOptions<MobilePushTokenHasherOptions> options)
    : IMobilePushTokenHasher
{
    private readonly byte[] _pepper = ResolvePepper(options.Value);

    public string? ComputeHash(string? deviceToken)
    {
        if (string.IsNullOrEmpty(deviceToken))
        {
            return null;
        }

        // Trim is sufficient — device tokens are case-sensitive (FCM/APNs both
        // emit base64url-style identifiers where case carries entropy).
        byte[] payload = Encoding.UTF8.GetBytes(deviceToken.Trim());
        byte[] digest = HMACSHA256.HashData(_pepper, payload);
        return Convert.ToHexStringLower(digest);
    }

    private static byte[] ResolvePepper(MobilePushTokenHasherOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DeviceTokenLookupPepper))
        {
            throw new InvalidOperationException(
                "Notifications:MobilePush:TokenHasher:DeviceTokenLookupPepper is required when " +
                "at-rest encryption is enabled on Granit.Notifications. The pepper must be at " +
                "least 32 bytes of high-entropy random data (e.g. openssl rand -hex 32) and " +
                "stored separately from the encryption key ring so the two can be rotated " +
                "independently.");
        }

        string pepper = options.DeviceTokenLookupPepper.Trim();
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
