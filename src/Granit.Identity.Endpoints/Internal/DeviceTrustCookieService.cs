using Granit.Guids;
using Granit.Http.Cookies;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Default <see cref="IDeviceTrustCookieService"/>. The signed token is produced with ASP.NET Core Data
/// Protection; the cookie itself is written through <see cref="IGranitCookieManager"/> so it goes through the
/// Strict Registry Pattern and GDPR consent gate (GRSEC004). Reads come straight off the request cookies.
/// </summary>
internal sealed class DeviceTrustCookieService(
    IDataProtectionProvider dataProtectionProvider,
    IGranitCookieManager cookieManager,
    IGuidGenerator guidGenerator,
    IOptions<DeviceTrustOptions> options) : IDeviceTrustCookieService
{
    private DeviceTrustOptions Options => options.Value;

    public string? ResolveDeviceId(HttpContext httpContext, string userId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Cookies.TryGetValue(Options.CookieName, out string? token)
            && !string.IsNullOrEmpty(token)
            && DeviceTrustToken.TryUnprotect(dataProtectionProvider, token) is { } payload
            && string.Equals(payload.UserId, userId, StringComparison.Ordinal))
        {
            return payload.DeviceId;
        }

        return null;
    }

    public async Task<string> IssueDeviceCookieAsync(HttpContext httpContext, string userId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Reuse the existing device id for this browser when the cookie is still valid, so re-trusting the same
        // device keeps a stable id; otherwise mint a fresh one.
        string deviceId = ResolveDeviceId(httpContext, userId) ?? guidGenerator.Create().ToString("N");

        string token = DeviceTrustToken.Protect(
            dataProtectionProvider,
            new DeviceTrustTokenPayload(userId, deviceId),
            Options.TrustDuration);

        await cookieManager.SetCookieAsync(httpContext, Options.CookieName, token).ConfigureAwait(false);
        return deviceId;
    }

    public void Clear(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        cookieManager.DeleteCookie(httpContext, Options.CookieName);
    }
}
