using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Unconditionally authenticates every request as a synthetic "admin" principal.
/// Combined with <see cref="PermissiveAuthorizationPolicyProvider"/>, this lets the
/// endpoint tests focus on the visibility-matrix / validation logic rather than the
/// permission-grant evaluation pipeline — permission enforcement is covered by
/// unit tests around <c>IPermissionChecker</c>.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, "test-admin"),
            new(ClaimTypes.Name, "test-admin"),
        ];
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
