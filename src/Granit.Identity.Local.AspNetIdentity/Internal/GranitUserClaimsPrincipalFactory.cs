using System.Security.Claims;
using Granit.Identity.Local.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Extends the default <see cref="UserClaimsPrincipalFactory{TUser, TRole}"/> to inject the
/// <c>tenant_id</c> claim into the Identity cookie principal. This allows multi-tenancy
/// middleware to resolve the tenant from the authenticated cookie on subsequent requests
/// (authorize, refresh, 2FA second step).
/// </summary>
internal sealed class GranitUserClaimsPrincipalFactory(
    UserManager<GranitUser> userManager,
    RoleManager<GranitRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<GranitUser, GranitRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(GranitUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);

        if (user.TenantId is not null)
        {
            identity.AddClaim(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        return identity;
    }
}
