using Granit.Identity.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Application-pipeline wiring for device trust.
/// </summary>
public static class DeviceTrustApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the device-trust resolution middleware, which decodes the signed device-trust cookie once per
    /// request into <c>HttpContext.Items</c> so the session-created emission sites can attach the device id.
    /// Place it before endpoint execution (e.g. alongside authentication). Optional: when absent, device trust
    /// still works for the manage endpoints and the login step-up bypass; only the federated risk signal degrades
    /// (sessions are treated as coming from an untrusted device).
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    public static IApplicationBuilder UseGranitDeviceTrust(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<DeviceTrustResolutionMiddleware>();
    }
}
