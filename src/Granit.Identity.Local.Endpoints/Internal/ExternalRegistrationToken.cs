using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Payload carried by the continuation token between the external-login callback and the
/// complete-registration endpoint. The provider key is sensitive — it must only ever travel inside
/// the encrypted token, never as a plaintext request field or query parameter.
/// </summary>
/// <param name="Provider">The external login provider name.</param>
/// <param name="ProviderKey">The provider-specific unique identifier.</param>
/// <param name="Email">The provider-verified email, if any. When present, completion must use the same email.</param>
/// <param name="FirstName">The first name returned by the provider, if any.</param>
/// <param name="LastName">The last name returned by the provider, if any.</param>
/// <param name="TenantId">The tenant the callback ran in. Re-checked at completion to prevent cross-tenant replay.</param>
internal sealed record ExternalRegistrationTokenPayload(
    string Provider,
    string ProviderKey,
    string? Email,
    string? FirstName,
    string? LastName,
    Guid? TenantId);

/// <summary>
/// Protects and validates the external-registration continuation token using ASP.NET Core Data
/// Protection with a short, enforced lifetime. The token authenticates the provider/provider-key
/// pair so the client never supplies them directly.
/// </summary>
internal static class ExternalRegistrationToken
{
    private const string ProtectorPurpose = "Granit.Identity.Local.ExternalRegistration.v1";

    /// <summary>How long a continuation token remains valid after the callback mints it.</summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Mints a time-limited, encrypted token for the given payload.</summary>
    public static string Protect(IDataProtectionProvider dataProtectionProvider, ExternalRegistrationTokenPayload payload)
    {
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        return protector.Protect(JsonSerializer.Serialize(payload), Lifetime);
    }

    /// <summary>
    /// Validates and decodes a token. Returns <see langword="null"/> when the token is malformed,
    /// tampered with, or expired.
    /// </summary>
    public static ExternalRegistrationTokenPayload? TryUnprotect(IDataProtectionProvider dataProtectionProvider, string token)
    {
        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
            string json = protector.Unprotect(token);
            return JsonSerializer.Deserialize<ExternalRegistrationTokenPayload>(json);
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
