using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.SenderConstraining;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Server.Internal;

/// <summary>
/// Fails fast when the configured sender-constraining mode is inconsistent with the mechanism
/// packages actually referenced: FAPI 2.0 requires a mechanism, and a declared mode must have
/// its package registered (otherwise the binding would silently never apply).
/// </summary>
internal sealed class SenderConstrainingOptionsValidator(IEnumerable<ISenderConstrainingMechanism> mechanisms)
    : IValidateOptions<GranitOpenIddictOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitOpenIddictOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EnableFapi2Profile && options.SenderConstraining == SenderConstrainingMode.None)
        {
            return ValidateOptionsResult.Fail(
                "FAPI 2.0 (EnableFapi2Profile) requires a sender-constraining mechanism. Set "
                + "OpenIddict:SenderConstraining and reference the mechanism package "
                + "(Granit.OpenIddict.Server.DPoP for DPoP).");
        }

        if (options.SenderConstraining != SenderConstrainingMode.None
            && !mechanisms.Any(m => m.Mode == options.SenderConstraining))
        {
            return ValidateOptionsResult.Fail(
                $"OpenIddict:SenderConstraining is '{options.SenderConstraining}' but no matching "
                + "sender-constraining mechanism is registered. Reference the corresponding package "
                + "(Granit.OpenIddict.Server.DPoP for DPoP).");
        }

        return ValidateOptionsResult.Success;
    }
}
