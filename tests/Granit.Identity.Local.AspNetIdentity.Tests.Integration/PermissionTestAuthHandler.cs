using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Test authentication handler driven by request headers so the permission-enforcement
/// tests can flip the caller identity without rebuilding the server:
/// <list type="bullet">
/// <item><c>X-Test-User-Id</c>: overrides the default <c>sub</c> claim when set.</item>
/// <item><c>X-Test-Roles</c>: comma-separated role claims.</item>
/// </list>
/// Returning <see cref="AuthenticateResult.NoResult"/> when neither header is present
/// lets the tests cover the unauthenticated → 401 path.
/// </summary>
internal sealed class PermissionTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PermissionTest";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string RolesHeader = "X-Test-Roles";
    public const string DefaultTestUserId = "test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        bool hasUserId = Request.Headers.TryGetValue(UserIdHeader, out Microsoft.Extensions.Primitives.StringValues userIdHeader);
        bool hasRoles = Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader);

        if (!hasUserId && !hasRoles)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string userId = hasUserId && !string.IsNullOrWhiteSpace(userIdHeader.ToString())
            ? userIdHeader.ToString()
            : DefaultTestUserId;

        string[] roles = hasRoles
            ? rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
            : [];

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Name, userId),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
