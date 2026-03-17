using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Features.Endpoints.Tests;

/// <summary>
/// Fake authentication handler that resolves authentication from the
/// <c>X-Test-Roles</c> header. When the header is present, the user is
/// considered authenticated with a fixed <c>sub</c> claim.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string TestUserId = "test-user-id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
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
