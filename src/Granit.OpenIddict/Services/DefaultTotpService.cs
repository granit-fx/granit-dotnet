using Granit.Identity.Local.Services;
using Granit.Timing;

namespace Granit.OpenIddict.Services;

/// <summary>
/// Lightweight RFC 6238 TOTP fallback used when the AspNet Identity layer (which reads the issuer
/// from <c>IdentityOptions.Tokens.AuthenticatorIssuer</c>) is not loaded.
/// </summary>
internal sealed class DefaultTotpService(IClock clock) : TotpServiceBase(clock)
{
    protected override string Issuer => "Granit";
}
