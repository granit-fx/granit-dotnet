using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Authenticates the request only when the <see cref="SubjectHeader"/> is present on
/// the incoming request, setting the header value as the <c>NameIdentifier</c> claim
/// (which <see cref="HttpContextCurrentUserService"/> reads as <c>sub</c>).
/// Absence of the header surfaces as a 401 through the auth pipeline.
/// </summary>
internal sealed class SubjectTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SubjectHeader = "X-Test-Sub";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubjectHeader, out Microsoft.Extensions.Primitives.StringValues values)
            || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string subject = values.ToString();
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim("sub", subject),
        ];
        var identity = new ClaimsIdentity(claims, TestAuthHandler.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestAuthHandler.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
