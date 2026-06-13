using Microsoft.AspNetCore.Http;

namespace Granit.Identity.Endpoints;

/// <summary>
/// Reads and writes the signed device-trust cookie that binds the current browser to a stable device identity.
/// The single seam other layers use to resolve "which device is this browser" — the login step-up decision and
/// the session-created emission sites consume it without re-implementing the cookie/Data-Protection plumbing.
/// </summary>
public interface IDeviceTrustCookieService
{
    /// <summary>
    /// Resolves the stable device id this browser is bound to for <paramref name="userId"/> from the signed
    /// cookie. Returns <see langword="null"/> when the cookie is absent, tampered with, expired, or bound to a
    /// different user.
    /// </summary>
    string? ResolveDeviceId(HttpContext httpContext, string userId);

    /// <summary>
    /// Ensures the current browser has a device binding for <paramref name="userId"/>: reuses the existing
    /// cookie's device id when valid, otherwise mints a new one, then (re)writes the signed cookie with the
    /// configured trust lifetime. Returns the stable device id.
    /// </summary>
    Task<string> IssueDeviceCookieAsync(HttpContext httpContext, string userId);

    /// <summary>Deletes the device-trust cookie (used when the current device's trust is revoked).</summary>
    void Clear(HttpContext httpContext);
}
