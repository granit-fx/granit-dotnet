using Granit.Http.Hosting.Cors.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Hosting.Cors.Internal;

/// <summary>
/// Applies the CORS middleware (default policy) at the head of the pipeline so that
/// registering the module is sufficient — before this filter existed, a host that
/// forgot <c>app.UseCors()</c> got a silent no-op: policies were configured and
/// validated but never enforced.
/// </summary>
/// <remarks>
/// The default policy carries no endpoint-specific metadata, so running before
/// routing is safe: the middleware answers preflights and stamps response headers
/// from the configured policy alone. Hosts that need custom ordering (e.g. between
/// <c>UseRouting</c> and <c>UseAuthorization</c> with per-endpoint policies) opt out
/// via <see cref="GranitCorsOptions.AutoRegisterMiddleware"/> and call
/// <c>UseCors()</c> themselves.
/// </remarks>
internal sealed partial class CorsStartupFilter(
    IOptions<GranitCorsOptions> options,
    ILogger<CorsStartupFilter> logger) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        if (!options.Value.AutoRegisterMiddleware)
        {
            LogManualWiring(logger);
            return next;
        }

        return app =>
        {
            LogAutoWired(logger);
            app.UseCors();
            next(app);
        };
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "CORS middleware auto-registered at the head of the pipeline (default policy).")]
    private static partial void LogAutoWired(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CORS middleware auto-registration is disabled (Http:Cors:AutoRegisterMiddleware=false) — call app.UseCors() manually or no CORS headers will be emitted.")]
    private static partial void LogManualWiring(ILogger logger);
}
