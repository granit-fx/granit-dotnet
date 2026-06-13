using System.Security.Cryptography;
using System.Text.Json;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Data-protected implementation of <see cref="ISessionReviewTokenService"/> — the same time-limited
/// <see cref="ITimeLimitedDataProtector"/> primitive as <c>DeviceTrustToken</c> / <c>ExternalRegistrationToken</c>.
/// Tampered or expired tokens validate to <see langword="null"/>. Registered by <c>AddGranitIdentityEndpoints</c>.
/// </summary>
internal sealed class DataProtectionSessionReviewTokenService(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SessionReviewOptions> options) : ISessionReviewTokenService
{
    private const string ProtectorPurpose = "Granit.Identity.SessionReview.v1";

    public string Issue(string userId, string sessionId, string? deviceId, string? country)
    {
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        string payload = JsonSerializer.Serialize(
            new SessionReviewTokenPayload(userId, sessionId, deviceId, country));
        return protector.Protect(payload, options.Value.TokenLifetime);
    }

    public SessionReviewTokenPayload? Validate(string token)
    {
        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
            return JsonSerializer.Deserialize<SessionReviewTokenPayload>(protector.Unprotect(token));
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
