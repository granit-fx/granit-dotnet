using Granit.Http.Cookies;
using Granit.Identity.Endpoints.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Registers the signed device-trust cookie with the Granit cookie registry so it can be written through
/// <see cref="IGranitCookieManager"/> (Strict Registry Pattern, GRSEC004). The cookie is
/// <see cref="CookieCategory.StrictlyNecessary"/> — a security control that binds the browser to a trusted
/// device, so it bypasses GDPR consent suppression.
/// </summary>
internal sealed class DeviceTrustCookieDefinitionContributor(IOptions<DeviceTrustOptions> options)
    : ICookieDefinitionContributor
{
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        DeviceTrustOptions value = options.Value;
        yield return new CookieDefinition(
            value.CookieName,
            CookieCategory.StrictlyNecessary,
            RetentionDays: Math.Max(1, (int)Math.Ceiling(value.TrustDuration.TotalDays)),
            IsHttpOnly: true,
            Purpose: "Binds this browser to a device the user trusts, for reduced sign-in friction.")
        {
            // Lax (not Strict): the cookie must accompany top-level navigation back to the login flow
            // (e.g. an OpenIddict front-channel redirect) for the trusted-device decision to apply.
            SameSite = SameSiteMode.Lax,
        };
    }
}
