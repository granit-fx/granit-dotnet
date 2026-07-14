using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Header-driven test authentication scheme for the #3005 $metadata
/// authorization-stance matrix: a request carrying <c>X-Test-User</c> is
/// authenticated as that user; without the header the handler returns
/// NoResult, so a <c>RequireAuthorization()</c> route challenges with
/// <c>401</c> while <c>AllowAnonymous()</c> routes stay reachable.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out Microsoft.Extensions.Primitives.StringValues user)
            || string.IsNullOrWhiteSpace(user.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, user.ToString())], Scheme.Name);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
