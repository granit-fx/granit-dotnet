using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.SharedConnection;

/// <summary>
/// Exposes the ASP.NET Core Identity <see cref="DbContext"/> instance associated
/// with the current request scope.
/// </summary>
/// <remarks>
/// <para>
/// Consumed by <c>IGranitRoleOrchestrator</c> to open a connection-scope transaction
/// shared with the host-application DbContext (which exposes
/// <c>IPermissionGrantDbContext</c>) — enabling true atomic dual writes across the
/// Identity schema (<c>AspNet*</c> tables, <c>GranitRole</c>) and the authorization
/// schema (<c>authorization_role_metadata</c>, <c>authorization_permission_grants</c>)
/// without escalating to a distributed transaction manager (MSDTC).
/// </para>
/// <para>
/// The returned context is the same scoped instance consumed by
/// <see cref="Microsoft.AspNetCore.Identity.RoleManager{T}"/> and
/// <see cref="Microsoft.AspNetCore.Identity.UserManager{T}"/>. Callers that need a
/// fresh context for shared-connection plumbing on the host side must use a factory
/// (see <see cref="IAuthorizationHostDbContextAccessor"/>) — but the Identity side
/// intentionally stays scoped because Identity's <c>RoleStore</c> / <c>UserStore</c>
/// bind to whatever scoped instance the DI container resolved at store construction.
/// </para>
/// </remarks>
public interface IIdentityDbContextAccessor
{
    /// <summary>The scoped Identity <see cref="DbContext"/> instance for the current request.</summary>
    DbContext DbContext { get; }
}
