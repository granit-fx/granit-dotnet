using Granit.Http.SecurityHeaders;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.ApiDocumentation.Internal;

/// <summary>
/// Relaxes the Content-Security-Policy on the Scalar interactive API
/// reference endpoint. The Scalar HTML bootstrap loads its own inline script,
/// inline styles, and woff2 fonts from <c>https://fonts.scalar.com</c> — all
/// of which the API-grade default CSP (<c>default-src 'none'</c>) blocks.
/// </summary>
/// <remarks>
/// Scoped strictly by the presence of <see cref="ScalarApiReferenceMetadata"/>
/// on the matched endpoint, so no other route inherits the relaxation. The
/// contributor is registered by <c>UseGranitApiDocumentation</c> only when
/// the dev/prod gate decides Scalar will actually be mapped.
/// </remarks>
internal sealed class ScalarCspContributor : ICspContributor
{
    public void Contribute(HttpContext context, CspBuilder builder)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<ScalarApiReferenceMetadata>() is null)
        {
            return;
        }

        builder
            .AddScriptSrc("'self'", "'unsafe-inline'")
            .AddStyleSrc("'self'", "'unsafe-inline'")
            .AddFontSrc("'self'", "data:", "https://fonts.scalar.com")
            .AddImgSrc("'self'", "data:", "https:")
            .AddConnectSrc("'self'");
    }
}
