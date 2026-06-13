using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Payload of the signed device-trust cookie: binds the current browser to a stable device identity for a
/// specific user. The device id never travels as a plaintext field — only inside this data-protected token.
/// </summary>
/// <param name="UserId">The subject the device binding is for; re-checked on read to prevent cross-user replay.</param>
/// <param name="DeviceId">The stable, server-minted device identifier this browser is bound to.</param>
internal sealed record DeviceTrustTokenPayload(string UserId, string DeviceId);

/// <summary>
/// Protects and validates the device-trust cookie token using ASP.NET Core Data Protection with an enforced
/// lifetime — the same primitive as <c>ExternalRegistrationToken</c>. Tampered, expired, or wrong-user tokens
/// resolve to <see langword="null"/>.
/// </summary>
internal static class DeviceTrustToken
{
    private const string ProtectorPurpose = "Granit.Identity.DeviceTrust.v1";

    /// <summary>Mints a time-limited, encrypted token binding <paramref name="payload"/> for <paramref name="lifetime"/>.</summary>
    public static string Protect(
        IDataProtectionProvider dataProtectionProvider,
        DeviceTrustTokenPayload payload,
        TimeSpan lifetime)
    {
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        return protector.Protect(JsonSerializer.Serialize(payload), lifetime);
    }

    /// <summary>
    /// Validates and decodes a token. Returns <see langword="null"/> when the token is malformed, tampered with,
    /// or expired.
    /// </summary>
    public static DeviceTrustTokenPayload? TryUnprotect(IDataProtectionProvider dataProtectionProvider, string token)
    {
        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
            return JsonSerializer.Deserialize<DeviceTrustTokenPayload>(protector.Unprotect(token));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
