using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.Testing.Endpoints;

/// <summary>
/// Authentication handler for endpoint tests. Resolves identity from the
/// <c>X-Test-Roles</c> header. When the header is absent, returns
/// <see cref="AuthenticateResult.NoResult"/> (the user is anonymous).
/// </summary>
/// <remarks>
/// Roles are comma-separated. Each value becomes a <see cref="ClaimTypes.Role"/>
/// claim. A fixed <c>sub</c> and <c>name</c> claim are also added so policies
/// requiring an authenticated user pass.
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string TestUserId = "test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out StringValues rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", TestUserId),
            new(ClaimTypes.Name, "test-user"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
