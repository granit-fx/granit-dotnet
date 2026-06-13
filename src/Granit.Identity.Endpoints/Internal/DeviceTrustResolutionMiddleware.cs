using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Resolves the signed device-trust cookie once per request and stashes the <c>(userId, deviceId)</c> binding
/// into <see cref="HttpContext.Items"/> under the <see cref="DeviceTrustContextItems"/> keys. This lets the
/// session-created emission sites (BFF, OpenIddict) attach the device id to the event without taking a
/// dependency on the cookie/Data-Protection machinery.
/// </summary>
internal sealed class DeviceTrustResolutionMiddleware(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<DeviceTrustOptions> options) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Request.Cookies.TryGetValue(options.Value.CookieName, out string? token)
            && !string.IsNullOrEmpty(token)
            && DeviceTrustToken.TryUnprotect(dataProtectionProvider, token) is { } payload)
        {
            context.Items[DeviceTrustContextItems.UserId] = payload.UserId;
            context.Items[DeviceTrustContextItems.DeviceId] = payload.DeviceId;
        }

        await next(context).ConfigureAwait(false);
    }
}
