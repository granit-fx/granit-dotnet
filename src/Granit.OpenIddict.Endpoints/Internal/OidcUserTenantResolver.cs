using System.Security.Claims;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Microsoft.AspNetCore.Identity;

namespace Granit.OpenIddict.Endpoints.Internal;

/// <summary>
/// Looks up the <see cref="LocalIdentity"/> behind an OIDC protocol request with the multi-tenant
/// query filter disabled.
/// </summary>
/// <remarks>
/// <para>
/// The user id (subject) and user name are globally unique and the request is already
/// authenticated, so the lookup must not depend on whatever tenant scope happens to be active —
/// otherwise a host user (<c>TenantId == null</c>) or a user whose tenant was not resolved for this
/// request becomes invisible and the flow fails closed with a spurious 403. This is the single
/// source of truth for the filter-disabled lookup used by the authorize, device-verification,
/// userinfo and two-factor/passkey-grant flows.
/// </para>
/// <para>
/// Aligning the ambient tenant scope to the resolved user (<c>currentTenant.Change(user.TenantId)</c>)
/// is deliberately left to the caller: <c>ICurrentTenant</c> is <c>AsyncLocal</c>-backed, so a scope
/// opened inside this async method would not flow back to the caller. The caller opens it in its own
/// execution context and keeps it for the rest of the request.
/// </para>
/// </remarks>
internal static class OidcUserTenantResolver
{
    /// <summary>Resolves the user by subject id (userinfo, passkey grant).</summary>
    public static Task<LocalIdentity?> FindBySubjectAsync(
        UserManager<LocalIdentity> userManager, string subject, IDataFilter? dataFilter)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        return WithoutTenantFilterAsync(dataFilter, () => userManager.FindByIdAsync(subject));
    }

    /// <summary>Resolves the user by user name (two-factor grant).</summary>
    public static Task<LocalIdentity?> FindByUserNameAsync(
        UserManager<LocalIdentity> userManager, string userName, IDataFilter? dataFilter)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        return WithoutTenantFilterAsync(dataFilter, () => userManager.FindByNameAsync(userName));
    }

    /// <summary>Resolves the user by authenticated principal (cookie-driven authorize/verify).</summary>
    public static Task<LocalIdentity?> FindByPrincipalAsync(
        UserManager<LocalIdentity> userManager, ClaimsPrincipal principal, IDataFilter? dataFilter)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        return WithoutTenantFilterAsync(dataFilter, () => userManager.GetUserAsync(principal));
    }

    /// <summary>
    /// Runs <paramref name="lookup"/> with the <see cref="IMultiTenant"/> filter disabled, restoring
    /// it afterwards. Extracted for unit testing without an ASP.NET Identity
    /// <see cref="UserManager{TUser}"/>.
    /// </summary>
    internal static async Task<LocalIdentity?> WithoutTenantFilterAsync(
        IDataFilter? dataFilter, Func<Task<LocalIdentity?>> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        IDisposable? filterScope = dataFilter?.Disable<IMultiTenant>();
        try
        {
            return await lookup().ConfigureAwait(false);
        }
        finally
        {
            filterScope?.Dispose();
        }
    }
}
