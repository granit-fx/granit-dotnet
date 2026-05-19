using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// RFC 6238 TOTP service reading the authenticator issuer from
/// <see cref="TokenOptions.AuthenticatorIssuer"/>, with a fallback to <c>"Granit"</c>.
/// </summary>
internal sealed class TotpService(
    IClock clock,
    IOptions<IdentityOptions> identityOptions) : TotpServiceBase(clock)
{
    protected override string Issuer => identityOptions.Value.Tokens.AuthenticatorIssuer ?? "Granit";
}
