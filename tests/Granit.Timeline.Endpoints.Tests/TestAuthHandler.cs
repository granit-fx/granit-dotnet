using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Fake authentication handler that resolves identity from the
/// <c>X-Test-Permissions</c> header (the permission names granted to the caller)
/// and, when present, the legacy <c>X-Test-Roles</c> header. Shared by all endpoint
/// test classes. When neither header is present the caller is anonymous.
/// </summary>
/// <remarks>
/// Authorization in Granit is permission-based, never role-based: policies gate on
/// <c>RequireClaim(<see cref="PermissionClaimType"/>, permissionName)</c>. Tests grant
/// permissions by listing them (comma-separated) in <see cref="PermissionsHeader"/>; each
/// becomes a <see cref="PermissionClaimType"/> claim. Role claims are still emitted from
/// <see cref="RolesHeader"/> but never gate endpoint authorization.
/// </remarks>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string UserIdHeader = "X-Test-UserId";

    /// <summary>Header carrying the comma-separated permission names granted to the test caller.</summary>
    public const string PermissionsHeader = "X-Test-Permissions";

    /// <summary>Claim type a permission name is emitted under; matched by <c>RequireClaim</c> in permission policies.</summary>
    public const string PermissionClaimType = "permission";

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

        string userId = Request.Headers.TryGetValue(UserIdHeader, out StringValues userIdHeader)
            ? userIdHeader.ToString()
            : "test-user-id";

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
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
