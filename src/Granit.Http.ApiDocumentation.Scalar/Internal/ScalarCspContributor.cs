using Granit.Http.ApiDocumentation.Options;
using Granit.Http.SecurityHeaders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Http.ApiDocumentation.Scalar.Internal;

/// <summary>
/// Relaxes the Content-Security-Policy on the Scalar interactive API
/// reference endpoint. The Scalar HTML bootstrap loads its own inline script,
/// inline styles, woff2 fonts from <c>https://fonts.scalar.com</c>, and
/// fetches its curated-documents / search registry from
/// <c>https://api.scalar.com</c> — all of which the API-grade default CSP
/// (<c>default-src 'none'</c>) blocks. The Scalar bundle also evaluates code
/// dynamically (template parsing, JSON Schema example rendering) via
/// <c>eval</c> / <c>new Function</c>, hence <c>'unsafe-eval'</c> on
/// <c>script-src</c>. When OAuth2 is configured, the origins (scheme + host
/// + port) of the authorization and token endpoints are added to
/// <c>connect-src</c> so the Authorization Code → Token exchange
/// (cross-origin XHR/fetch from the Scalar SPA to the IdP) is not blocked.
/// </summary>
/// <remarks>
/// Scoped strictly by the presence of <see cref="ScalarApiReferenceMetadata"/>
/// on the matched endpoint, so no other route inherits the relaxation. The
/// contributor is registered by <c>UseGranitApiDocumentation</c> only when
/// the dev/prod gate decides Scalar will actually be mapped — and Scalar
/// itself is dev-only by default (<c>ScalarOptions.EnableInProduction</c>
/// defaults to <c>false</c>), so the <c>'unsafe-eval'</c> / <c>'unsafe-inline'</c>
/// relaxations stay confined to a single non-production route. The rest of
/// the application keeps the strict <c>default-src 'none'</c> baseline.
/// </remarks>
internal sealed class ScalarCspContributor(IOptions<ApiDocumentationOptions> options) : ICspContributor
{
    private const string SelfSource = "'self'";

    private readonly ApiDocumentationOptions _options = options.Value;

    public void Contribute(HttpContext context, CspBuilder builder)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<ScalarApiReferenceMetadata>() is null)
        {
            return;
        }

        List<string> connectSources = [SelfSource, "https://api.scalar.com"];
        AddOrigin(connectSources, _options.OAuth2.AuthorizationUrl);
        AddOrigin(connectSources, _options.OAuth2.TokenUrl);

        builder
            .AddScriptSrc(SelfSource, "'unsafe-inline'", "'unsafe-eval'")
            .AddStyleSrc(SelfSource, "'unsafe-inline'")
            .AddFontSrc(SelfSource, "data:", "https://fonts.scalar.com")
            .AddImgSrc(SelfSource, "data:", "https:")
            .AddConnectSrc([.. connectSources.Distinct()]);
    }

    private static void AddOrigin(List<string> list, string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            list.Add($"{uri.Scheme}://{uri.Authority}");
        }
    }
}
