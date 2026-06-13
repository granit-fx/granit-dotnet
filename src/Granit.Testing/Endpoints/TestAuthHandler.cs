using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.Testing.Endpoints;

/// <summary>
/// Authentication handler for endpoint tests. Resolves identity from the
/// <c>X-Test-Permissions</c> header (the permission names granted to the caller)
/// and, when present, the legacy <c>X-Test-Roles</c> header. When neither header
/// is present, returns <see cref="AuthenticateResult.NoResult"/> (anonymous).
/// </summary>
/// <remarks>
/// <para>
/// Authorization in Granit is <strong>permission-based, never role-based</strong> — endpoints gate on
/// <c>RequireAuthorization(SomePermission)</c>, never <c>RequireRole</c>. Tests grant permissions by
/// sending their names (comma-separated) in <see cref="PermissionsHeader"/>; each becomes a
/// <see cref="PermissionClaimType"/> claim, which a permission policy satisfies with
/// <c>RequireClaim(TestAuthHandler.PermissionClaimType, permissionName)</c>.
/// </para>
/// <para>
/// A fixed <c>sub</c> and <c>name</c> claim are always added so policies requiring an authenticated user
/// pass. Role claims are still emitted from <see cref="RolesHeader"/> for the few tests that assert on the
/// role claim itself — never wire endpoint authorization to them.
/// </para>
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    /// <summary>Header carrying the comma-separated permission names granted to the test caller.</summary>
    public const string PermissionsHeader = "X-Test-Permissions";

    /// <summary>Claim type a permission name is emitted under; matched by <c>RequireClaim</c> in permission policies.</summary>
    public const string PermissionClaimType = "permission";

    public const string TestUserId = "test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        bool hasRoles = Request.Headers.TryGetValue(RolesHeader, out StringValues rolesHeader);
        bool hasPermissions = Request.Headers.TryGetValue(PermissionsHeader, out StringValues permsHeader);

        if (!hasRoles && !hasPermissions)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        string[] permissions = permsHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", TestUserId),
            new(ClaimTypes.Name, "test-user"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
            .. permissions.Select(p => new Claim(PermissionClaimType, p.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
