using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Fails startup when <see cref="IdentityOptions.User"/>.<c>RequireUniqueEmail</c> is
/// disabled while no <c>ITenantResolver</c> is registered.
/// </summary>
/// <remarks>
/// <para>
/// The headless login and 2FA handlers disable the multi-tenant query filter when no
/// tenant context is active so they can resolve a user across tenants by email.
/// That cross-tenant lookup relies on <c>RequireUniqueEmail = true</c> to guarantee a
/// single deterministic match. Without unique emails, multiple users with the same
/// address can exist across tenants and the lookup becomes non-deterministic — an
/// attacker who knows a victim's email can occasionally land in a different tenant's
/// account, target the wrong lockout counter, or trick the 2FA challenge into
/// resolving the wrong user.
/// </para>
/// <para>
/// When at least one <c>ITenantResolver</c> is registered, the cross-tenant lookup
/// is bypassed (the tenant context is set before login), so non-unique emails are safe.
/// This validator therefore only fails when both conditions are violated.
/// </para>
/// </remarks>
internal sealed class RequireUniqueEmailValidator(IServiceProvider services)
    : IValidateOptions<IdentityOptions>
{
    private const string TenantResolverTypeName = "Granit.MultiTenancy.Resolvers.ITenantResolver";

    public ValidateOptionsResult Validate(string? name, IdentityOptions options)
    {
        if (options.User.RequireUniqueEmail)
        {
            return ValidateOptionsResult.Success;
        }

        if (HasTenantResolverRegistered())
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "IdentityOptions.User.RequireUniqueEmail is false but no ITenantResolver is " +
            "registered. The cross-tenant login/2FA lookup that disables the IMultiTenant " +
            "filter cannot deterministically resolve a user when emails are not unique. " +
            "Either set IdentityOptions.User.RequireUniqueEmail = true (recommended) or " +
            "register an ITenantResolver so the tenant context is established before login.");
    }

    private bool HasTenantResolverRegistered()
    {
        // Soft dependency on Granit.MultiTenancy — resolved by full type name so this
        // module does not need a hard project reference. When Granit.MultiTenancy is
        // not loaded into the app domain, the type lookup returns null and the check
        // fails closed (no tenant resolver assumed).
        var tenantResolverType = Type.GetType(
            $"{TenantResolverTypeName}, Granit.MultiTenancy",
            throwOnError: false);

        return tenantResolverType is not null
            && services.GetService(tenantResolverType) is not null;
    }
}
