using Granit.Authentication.DPoP.Options;
using Granit.OpenIddict.Options;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Server.Internal;

/// <summary>
/// Forces the DPoP validation flags mandated by the FAPI 2.0 Security Profile when
/// <see cref="GranitOpenIddictOptions.EnableFapi2Profile"/> is enabled.
/// </summary>
/// <remarks>
/// FAPI 2.0 §5.3.2 requires sender-constrained access tokens. The middleware enforces
/// the binding at the resource side; this post-configurator pins the corresponding
/// options so a misconfigured app cannot relax the profile by leaving these flags
/// at their backwards-compatible defaults.
/// </remarks>
internal sealed class Fapi2DPoPOptionsConfigurator(IOptions<GranitOpenIddictOptions> openIddictOptions)
    : IPostConfigureOptions<DPoPValidationOptions>
{
    public void PostConfigure(string? name, DPoPValidationOptions options)
    {
        if (!openIddictOptions.Value.EnableFapi2Profile)
        {
            return;
        }

        options.RequireDPoP = true;
        options.RequireTokenBinding = true;
        options.RequireNonce = true;
    }
}
