using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Granit.Http.ApiDocumentation.Internal;

/// <summary>
/// Inserts <see cref="DeprecationHeadersMiddleware"/> into the pipeline so that
/// RFC 8594 deprecation headers are emitted from <c>DeprecatedAttribute</c> endpoint
/// metadata without any explicit <c>Use*</c> call in the host.
/// </summary>
internal sealed class DeprecationHeadersStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<DeprecationHeadersMiddleware>();
            next(app);
        };
}
